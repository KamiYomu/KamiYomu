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
    public class ChapterDiscoveryJob(
        ILogger<ChapterDiscoveryJob> logger,
        IOptions<WorkerOptions> workerOptions,
        ICrawlerAgentRepository agentCrawlerRepository,
        IHangfireRepository hangfireRepository,
        DbContext dbContext) : IChapterDiscoveryJob
    {
        private readonly WorkerOptions _workerOptions = workerOptions.Value;

        public async Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, PerformContext context, CancellationToken cancellationToken)
        {
            var userPreference = dbContext.UserPreferences.FindOne(p => true);
            var culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Dispatch cancelled {JobName}", nameof(ChapterDiscoveryJob));
                return;
            }

            var library = dbContext.Libraries.FindById(libraryId);

            if (library == null)
            {
                logger.LogWarning("{Dispatch} for \"{libraryId}\" could not proceed — the associated library record no longer exists.", nameof(DispatchAsync), libraryId);
                return;
            }

            using var libDbContext = library.GetDbContext();
            var mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == libraryId);
            var files = Directory.GetFiles(library.Manga.GetDirectory(), "*.cbz", SearchOption.AllDirectories);


            using var crawlerAgent = mangaDownload.Library.CrawlerAgent;
            var mangaId = mangaDownload.Library.Manga!.Id;

            int offset = 0;
            const int limit = 100;
            int? total = null;

            logger.LogInformation("Starting {jobname} for manga: {MangaId}", nameof(ChapterDiscoveryJob), mangaId);

            do
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("Dispatch cancelled during chapter fetch for manga: {MangaId}", mangaId);
                    mangaDownload.Cancelled($"Cancelled during the running job: {mangaId}");
                    libDbContext.MangaDownloadRecords.Update(mangaDownload);
                    return;
                }

                var page = await agentCrawlerRepository.GetMangaChaptersAsync(
                    crawlerAgent.Id, mangaId, new PaginationOptions(offset, limit), cancellationToken);

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
                    var queueState = hangfireRepository.GetLeastLoadedDownloadChapterQueue();
                    var backgroundJobId = BackgroundJob.Enqueue<IChapterDownloaderJob>(queueState.Queue, p => p.DispatchAsync(queueState.Queue, library.CrawlerAgent.Id, library.Id, mangaDownload.Id, record.Id, chapter.GetCbzFileName(), null!, CancellationToken.None));

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
