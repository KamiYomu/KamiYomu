using Hangfire;
using Hangfire.Storage;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Services;

/// <summary>
/// 
/// </summary>
/// <param name="workerOptions"></param>
/// <param name="hangfireRepository"></param>
/// <param name="jobClient"></param>
public class WorkerService(IOptions<WorkerOptions> workerOptions,
                           IHangfireRepository hangfireRepository,
                           IBackgroundJobClient jobClient) : IWorkerService
{
    /// <inheritdoc/>
    public string ScheduleMangaDownload(MangaDownloadRecord mangaDownloadRecord)
    {

        Hangfire.States.EnqueuedState mangaDownloadQueueState = hangfireRepository.GetLeastLoadedMangaDownloadSchedulerQueue();

        string backgroundJobId = BackgroundJob.Enqueue<IMangaDownloaderJob>(mangaDownloadQueueState.Queue, p => p.DispatchAsync(mangaDownloadQueueState.Queue, mangaDownloadRecord.Library.CrawlerAgent.Id, mangaDownloadRecord.Library.Id, mangaDownloadRecord.Id, mangaDownloadRecord.Library.Manga.Title, null!, CancellationToken.None));

        ScheduleDiscoverRecurringJob(mangaDownloadRecord.Library);

        return backgroundJobId;
    }

    /// <inheritdoc/>
    public void CancelMangaDownload(MangaDownloadRecord mangaDownloadRecord)
    {
        using LibraryDbContext libDbContext = mangaDownloadRecord.Library.GetReadWriteDbContext();

        mangaDownloadRecord.Cancelled("User remove manga from the library.");

        if (!string.IsNullOrWhiteSpace(mangaDownloadRecord.BackgroundJobId))
        {
            _ = jobClient.Delete(mangaDownloadRecord.BackgroundJobId);
        }

        IEnumerable<ChapterDownloadRecord> chapterDownloads = libDbContext.ChapterDownloadRecords.FindAll();

        foreach (ChapterDownloadRecord chapterDownload in chapterDownloads)
        {
            if (!string.IsNullOrWhiteSpace(chapterDownload.BackgroundJobId))
            {
                _ = jobClient.Delete(chapterDownload.BackgroundJobId);
            }
        }

        RemoveDiscoverRecurringJob(mangaDownloadRecord.Library);

        _ = libDbContext.MangaDownloadRecords.Update(mangaDownloadRecord);
    }

    /// <inheritdoc/>
    public void RemoveDiscoverRecurringJob(Library library)
    {
        RecurringJob.RemoveIfExists(library.GetDiscovertyJobId());
    }

    /// <inheritdoc/>
    public void ScheduleDiscoverRecurringJob(Library library)
    {
        string mangaDiscoveryQueue = workerOptions.Value.DiscoveryNewChapterQueues.First();

        RecurringJob.AddOrUpdate<IChapterDiscoveryJob>(library.GetDiscovertyJobId(), (job) => job.DispatchAsync(mangaDiscoveryQueue, library.CrawlerAgent.Id, library.Id, null!, CancellationToken.None), Cron.Daily());
    }

    /// <inheritdoc/>
    public string TriggerDiscoverRecurringJob(Library library)
    {
        string mangaDiscoveryQueue = workerOptions.Value.DiscoveryNewChapterQueues.First();

        string jobId = BackgroundJob.Enqueue<IChapterDiscoveryJob>((job) => job.DispatchAsync(mangaDiscoveryQueue, library.CrawlerAgent.Id, library.Id, null!, CancellationToken.None));

        return jobId;
    }

    /// <inheritdoc/>
    public bool IsDiscoverRecurringJobScheduled(Library library)
    {
        using IStorageConnection connection = JobStorage.Current.GetConnection();

        List<RecurringJobDto> recurringJobs = connection.GetRecurringJobs();

        return recurringJobs.Any(job => string.Equals(job.Id, library.GetDiscovertyJobId(), StringComparison.OrdinalIgnoreCase));
    }

}
