using Hangfire;
using Hangfire.Server;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace KamiYomu.Web.Worker;

public class MangaDownloaderJob : IMangaDownloaderJob
{
    private readonly ILogger<MangaDownloaderJob> _logger;
    private readonly Settings.Worker _workerOptions;
    private readonly DbContext _dbContext;
    private readonly IAgentCrawlerRepository _agentCrawlerRepository;
    private readonly IBackgroundJobClient _jobClient;
    private readonly IHangfireRepository _hangfireRepository;

    public MangaDownloaderJob(
        ILogger<MangaDownloaderJob> logger,
        IOptions<Settings.Worker> workerOptions,
        DbContext dbContext,
        IAgentCrawlerRepository agentCrawlerRepository,
        IBackgroundJobClient jobClient,
        IHangfireRepository hangfireRepository)
    {

        _logger = logger;
        _workerOptions = workerOptions.Value;
        _dbContext = dbContext;
        _agentCrawlerRepository = agentCrawlerRepository;
        _jobClient = jobClient;
        _hangfireRepository = hangfireRepository;
    }

    public async Task DispatchAsync(Guid libraryId, Guid mangaDownloadId, string title, PerformContext context, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Dispatch cancelled before processing manga: {mangaDownloadId}", mangaDownloadId);
            return;
        }
       
        var library = _dbContext.Libraries.FindById(libraryId);

        if (library == null) {
            _logger.LogError("Library was not found: {libraryId}", libraryId);
            throw new ArgumentException("Library was not found");
        }
        using var libDbContext = library.GetDbContext();

        var mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Id == mangaDownloadId && p.DownloadStatus == Entities.Definitions.DownloadStatus.Pending);
        if (mangaDownload == null) return;
        try
        {
            _logger.LogInformation("Dispatch started. JobId: {JobId}", context.BackgroundJob?.Id);


            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Dispatch cancelled before processing manga: {MangaId}", mangaDownload.Library.Manga.Id);
                return;
            }

            mangaDownload.Processing();

            libDbContext.MangaDownloadRecords.Update(mangaDownload);

            var agentCrawler = mangaDownload.Library.AgentCrawler;
            var mangaId = mangaDownload.Library.Manga.Id;

            int offset = 0;
            const int limit = 30;
            int? total = null;

            _logger.LogInformation("Starting dispatch for manga: {MangaId}", mangaId);

            do
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Dispatch cancelled during chapter fetch for manga: {MangaId}", mangaId);
                    mangaDownload.Cancelled($"Cancelled during the running job: {mangaId}");
                    libDbContext.MangaDownloadRecords.Update(mangaDownload);
                    return;
                }

                var page = await _agentCrawlerRepository.GetMangaChaptersAsync(
                    agentCrawler, mangaId, new PaginationOptions(offset, limit), cancellationToken);

                total = page.PaginationOptions.Total;

                foreach (var chapter in page.Data)
                {
                    var record = new ChapterDownloadRecord(agentCrawler, mangaDownload, chapter);
                    libDbContext.ChapterDownloadRecords.Insert(record);

                    var backgroundJobId = _jobClient.Create<IChapterDownloaderJob>(
                          p => p.DispatchAsync(library.Id, mangaDownload.Id, record.Id, $"v{chapter.Volume}-ch{chapter.Number}-{chapter.Title}", null!, CancellationToken.None),
                          _hangfireRepository.GetLeastLoadedCrawlerQueue()
                     );

                    record.Scheduled(backgroundJobId);
                    libDbContext.ChapterDownloadRecords.Update(record);
                    await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
                }

                offset += limit;
                await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
            } while (offset < total);

            _logger.LogInformation("Finished dispatch for manga: {MangaId}. Total chapters: {Total}", mangaId, total);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatch completed with error {Message}.", ex.Message);
            mangaDownload.Pending();
            libDbContext.MangaDownloadRecords.Update(mangaDownload);
            throw;
        }
    }
}
