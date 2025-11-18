using Hangfire;
using Hangfire.Server;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace KamiYomu.Web.Worker;

public class MangaDownloaderJob : IMangaDownloaderJob
{
    private readonly ILogger<MangaDownloaderJob> _logger;
    private readonly WorkerOptions _workerOptions;
    private readonly DbContext _dbContext;
    private readonly IAgentCrawlerRepository _agentCrawlerRepository;
    private readonly IBackgroundJobClient _jobClient;
    private readonly IHangfireRepository _hangfireRepository;
    private readonly INotificationService _notificationService;

    public MangaDownloaderJob(
        ILogger<MangaDownloaderJob> logger,
        IOptionsSnapshot<WorkerOptions> workerOptions,
        DbContext dbContext,
        IAgentCrawlerRepository agentCrawlerRepository,
        IBackgroundJobClient jobClient,
        IHangfireRepository hangfireRepository,
        INotificationService notificationService)
    {

        _logger = logger;
        _workerOptions = workerOptions.Value;
        _dbContext = dbContext;
        _agentCrawlerRepository = agentCrawlerRepository;
        _jobClient = jobClient;
        _hangfireRepository = hangfireRepository;
        _notificationService = notificationService;
    }

    public async Task DispatchAsync(Guid crawlerId, Guid libraryId, Guid mangaDownloadId, string title, PerformContext context, CancellationToken cancellationToken)
    {
        var userPreference = _dbContext.UserPreferences.FindOne(p => true);
        var culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var library = _dbContext.Libraries.FindById(libraryId);
        if(library == null)
        {
            _logger.LogError("Library no longer exists");
            return;
        }
        using var libDbContext = library.GetDbContext();
        var mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Id == mangaDownloadId);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (library == null)
            {
                _logger.LogWarning("Dispatch \"{title}\" could not proceed — the associated library record no longer exists.", title);
                return;
            }

            if (mangaDownload.DownloadStatus != Entities.Definitions.DownloadStatus.Scheduled &&
                mangaDownload.DownloadStatus != Entities.Definitions.DownloadStatus.Pending)
            {
                return;
            }

            _logger.LogInformation("Dispatch started. JobId: {JobId}", context.BackgroundJob?.Id);


            cancellationToken.ThrowIfCancellationRequested();

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
                cancellationToken.ThrowIfCancellationRequested();

                var page = await _agentCrawlerRepository.GetMangaChaptersAsync(
                    agentCrawler, mangaId, new PaginationOptions(offset, limit), cancellationToken);

                total = page.PaginationOptions.Total;

                foreach (var chapter in page.Data)
                {
                    var record = new ChapterDownloadRecord(agentCrawler, mangaDownload, chapter);
                    libDbContext.ChapterDownloadRecords.Insert(record);

                    var backgroundJobId = _jobClient.Create<IChapterDownloaderJob>(
                          p => p.DispatchAsync(library.AgentCrawler.Id, library.Id, mangaDownload.Id, record.Id, chapter.GetCbzFileName(), null!, CancellationToken.None),
                          _hangfireRepository.GetLeastLoadedDownloadChapterQueue()
                     );

                    record.Scheduled(backgroundJobId);
                    libDbContext.ChapterDownloadRecords.Update(record);
                    await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
                }

                offset += limit;
                await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
            } while (offset < total);

            _logger.LogInformation("Finished dispatch for manga: {MangaId}. Total chapters: {Total}", mangaId, total);
            mangaDownload.Complete();

            libDbContext.MangaDownloadRecords.Update(mangaDownload);

            if (userPreference.FamilySafeMode && mangaDownload.Library.Manga.IsFamilySafe || !userPreference.FamilySafeMode)
            {
                await _notificationService.PushSuccessAsync($"{mangaDownload.Library.Manga.Title}: {I18n.SearchForChaptersCompleted}.", cancellationToken);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dispatch completed with error {Message}.", ex.Message);
            mangaDownload.Pending(ex.Message);
            libDbContext.MangaDownloadRecords.Update(mangaDownload);
            throw;
        }
    }
}
