using Hangfire;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Services
{
    public class WorkerService(IOptions<WorkerOptions> workerOptions, 
                               IHangfireRepository hangfireRepository, 
                               IBackgroundJobClient jobClient,
                               CacheContext cacheContext,
                               DbContext dbContext) : IWorkerService
    {
       
        public string ScheduleMangaDownload(MangaDownloadRecord mangaDownloadRecord)
        {
            cacheContext.Current.Empty(mangaDownloadRecord.Library.CrawlerAgent.GetConcurrencyCacheKey());

            var mangaDownloadQueueState = hangfireRepository.GetLeastLoadedMangaDownloadSchedulerQueue();
            
            var backgroundJobId = BackgroundJob.Enqueue<IMangaDownloaderJob>(mangaDownloadQueueState.Queue, p => p.DispatchAsync(mangaDownloadQueueState.Queue, mangaDownloadRecord.Library.CrawlerAgent.Id, mangaDownloadRecord.Library.Id, mangaDownloadRecord.Id, mangaDownloadRecord.Library.Manga.Title, null!, CancellationToken.None));

            var mangaDiscoveryQueue = workerOptions.Value.DiscoveryNewChapterQueues.First();

            RecurringJob.AddOrUpdate<IChapterDiscoveryJob>(mangaDownloadRecord.Library.GetDiscovertyJobId(), (job) => job.DispatchAsync(mangaDiscoveryQueue, mangaDownloadRecord.Library.CrawlerAgent.Id, mangaDownloadRecord.Library.Id, null!, CancellationToken.None), Cron.Daily());

            return backgroundJobId;
        }

        public void CancelMangaDownload(MangaDownloadRecord mangaDownloadRecord)
        {
            using var libDbContext = mangaDownloadRecord.Library.GetDbContext();

            cacheContext.Current.Empty(mangaDownloadRecord.Library.CrawlerAgent.GetConcurrencyCacheKey());

            mangaDownloadRecord.Cancelled("User remove manga from the library.");

            if (!string.IsNullOrWhiteSpace(mangaDownloadRecord.BackgroundJobId))
            {
                jobClient.Delete(mangaDownloadRecord.BackgroundJobId);
            }

            var chapterDownloads = libDbContext.ChapterDownloadRecords.FindAll();

            foreach (var chapterDownload in chapterDownloads)
            {
                if (!string.IsNullOrWhiteSpace(chapterDownload.BackgroundJobId))
                {
                    jobClient.Delete(chapterDownload.BackgroundJobId);
                }
            }

            RecurringJob.RemoveIfExists(mangaDownloadRecord.Library.GetDiscovertyJobId());

            libDbContext.MangaDownloadRecords.Update(mangaDownloadRecord);
        }
    }
}
