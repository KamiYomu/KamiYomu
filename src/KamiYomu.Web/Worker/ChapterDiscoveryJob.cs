using Hangfire;
using Hangfire.Server;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace KamiYomu.Web.Worker
{
    public class ChapterDiscoveryJob : IChapterDiscoveryJob
    {
        private readonly ILogger<ChapterDiscoveryJob> _logger;
        private readonly WorkerOptions _workerOptions;
        private readonly IBackgroundJobClient _jobClient;
        private readonly ICrawlerAgentRepository _agentCrawlerRepository;
        private readonly IHangfireRepository _hangfireRepository;
        private readonly DbContext _dbContext;

        public ChapterDiscoveryJob(
            ILogger<ChapterDiscoveryJob> logger,
            IOptions<WorkerOptions> workerOptions,
            IBackgroundJobClient jobClient,
            ICrawlerAgentRepository agentCrawlerRepository,
            IHangfireRepository hangfireRepository,
            DbContext dbContext)
        {
            _logger = logger;
            _workerOptions = workerOptions.Value;
            _jobClient = jobClient;
            _agentCrawlerRepository = agentCrawlerRepository;
            _hangfireRepository = hangfireRepository;
            _dbContext = dbContext;
        }

        public async Task DispatchAsync(Guid crawlerId, Guid libraryId, PerformContext context, CancellationToken cancellationToken)
        {
            var userPreference = _dbContext.UserPreferences.FindOne(p => true);
            var culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Dispatch cancelled {JobName}", nameof(ChapterDiscoveryJob));
                return;
            }

            var library = _dbContext.Libraries.FindById(libraryId);

            if (library == null)
            {
                _logger.LogWarning("{Dispatch} for \"{libraryId}\" could not proceed — the associated library record no longer exists.", nameof(DispatchAsync), libraryId);
                return;
            }

            using var libDbContext = library.GetDbContext();
            var mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == libraryId);
            var files = Directory.GetFiles(library.Manga.GetDirectory(), "*.cbz", SearchOption.AllDirectories);


            using var crawlerAgent = mangaDownload.Library.AgentCrawler;
            var mangaId = mangaDownload.Library.Manga!.Id;

            int offset = 0;
            const int limit = 100;
            int? total = null;

            _logger.LogInformation("Starting {jobname} for manga: {MangaId}", nameof(ChapterDiscoveryJob), mangaId);

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
                    crawlerAgent, mangaId, new PaginationOptions(offset, limit), cancellationToken);

                total = page.PaginationOptions.Total;

                foreach (var chapter in page.Data)
                {
                    if (File.Exists(chapter.GetCbzFilePath()))
                    {
                        continue;
                    }

                    var record = libDbContext.ChapterDownloadRecords
                                             .FindOne(p => p.Chapter!.Id == chapter.Id
                                                        && p.CrawlerAgent!.Id == crawlerAgent.Id)
                                             ?? new ChapterDownloadRecord(crawlerAgent, mangaDownload, chapter);

                    if (record.IsInProgress() || (record.IsCompleted() && record.LastUpdatedStatusTotalDays() < 1))
                    {
                        continue;
                    }

                    record.ToBeRescheduled();

                    libDbContext.ChapterDownloadRecords.Upsert(record);

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
        }
    }
}
