using Hangfire;
using Hangfire.Server;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace KamiYomu.Web.Worker
{
    public class ChapterDiscoveryJob : IChapterDiscoveryJob
    {
        private readonly ILogger<ChapterDiscoveryJob> _logger;
        private readonly Settings.Worker _workerOptions;
        private readonly IBackgroundJobClient _jobClient;
        private readonly IAgentCrawlerRepository _agentCrawlerRepository;
        private readonly IHangfireRepository _hangfireRepository;
        private readonly DbContext _dbContext;

        public ChapterDiscoveryJob(
            ILogger<ChapterDiscoveryJob> logger,
            IOptions<Settings.Worker> workerOptions,
            IBackgroundJobClient jobClient,
            IAgentCrawlerRepository agentCrawlerRepository,
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

        public async Task DispatchAsync(PerformContext context, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Dispatch cancelled {JobName}", nameof(ChapterDiscoveryJob));
                return;
            }

            var userPreference = _dbContext.UserPreferences.FindOne(p => true);

            Thread.CurrentThread.CurrentCulture =
            Thread.CurrentThread.CurrentUICulture =
            CultureInfo.CurrentCulture =
            CultureInfo.CurrentUICulture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

            var libraries = _dbContext.Libraries.FindAll();

            foreach (var library in libraries)
            {
                using var libDbContext = library.GetDbContext();
                var mangaDownloads = libDbContext.MangaDownloadRecords.FindAll();
                var files = Directory.GetFiles(library!.Manga!.GetDirectory(), "*.cbz", SearchOption.AllDirectories);

                foreach (var mangaDownload in mangaDownloads)
                {
                    var crawlerAgent = mangaDownload.Library.AgentCrawler;
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

                            if (record.IsInProgress() || (record.IsCompleted() && record.LastUpdatedStatusTotalHours() < _workerOptions.ChapterDiscoveryIntervalInHours))
                            {
                                continue;
                            }

                            record.Pending();

                            libDbContext.ChapterDownloadRecords.Upsert(record);

                            var backgroundJobId = _jobClient.Create<IChapterDownloaderJob>(
                                  p => p.DispatchAsync(library.Id, mangaDownload.Id, record.Id, chapter.GetCbzFileName(), null!, CancellationToken.None),
                                  _hangfireRepository.GetLeastLoadedCrawlerQueue()
                             );

                            record.Scheduled(backgroundJobId);
                            libDbContext.ChapterDownloadRecords.Update(record);
                            await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
                        }

                        offset += limit;
                        await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
                    } while (offset < total);
                    await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
                }
            }

        }
    }
}
