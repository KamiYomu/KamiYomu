using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Services;

/// <summary>
/// 
/// </summary>
/// <param name="logger"></param>
/// <param name="workerOptions"></param>
/// <param name="hangfireRepository"></param>
/// <param name="jobClient"></param>
public class WorkerService(ILogger<WorkerService> logger,
                           IOptions<WorkerOptions> workerOptions,
                           IHangfireRepository hangfireRepository,
                           IBackgroundJobClient jobClient) : IWorkerService
{
    /// <inheritdoc/>
    public string ScheduleMangaDownload(MangaDownloadRecord mangaDownloadRecord, TimeSpan? schedule)
    {

        Hangfire.States.EnqueuedState mangaDownloadQueueState = hangfireRepository.GetLeastLoadedMangaDownloadSchedulerQueue();

        string jobId = BackgroundJob.Enqueue<IMangaDownloaderJob>(mangaDownloadQueueState.Queue, p => p.DispatchAsync(mangaDownloadQueueState.Queue, mangaDownloadRecord.Library.CrawlerAgent.Id, mangaDownloadRecord.Library.Id, mangaDownloadRecord.Id, mangaDownloadRecord.Library.Manga.Title, null!, CancellationToken.None));

        using IStorageConnection connection = JobStorage.Current.GetConnection();

        connection.SetJobParameter(jobId, Defaults.Worker.LibraryId, mangaDownloadRecord.Library.Id.ToString());
        connection.SetJobParameter(jobId, Defaults.Worker.CrawlerAgentId, mangaDownloadRecord.Library.CrawlerAgent.Id.ToString());

        ScheduleDiscoverRecurringJob(mangaDownloadRecord.Library, schedule);

        return jobId;
    }

    /// <inheritdoc/>
    public TimeSpan? GetDiscoverySchedule(Library library)
    {
        using IStorageConnection connection = JobStorage.Current.GetConnection();

        RecurringJobDto? discoveryJob = connection.GetRecurringJobs()
                                                  .FirstOrDefault(job => string.Equals(job.Id, library.GetDiscovertyJobId(), StringComparison.OrdinalIgnoreCase));

        return !string.IsNullOrWhiteSpace(discoveryJob?.Cron) ? HangfireExtensions.ConvertCronDailyToTimeSpan(discoveryJob.Cron) : null;
    }

    /// <inheritdoc/>
    public void CancelMangaDownload(MangaDownloadRecord mangaDownloadRecord)
    {
        using LibraryDbContext libDbContext = mangaDownloadRecord.Library.GetReadOnlyDbContext();

        if (!string.IsNullOrWhiteSpace(mangaDownloadRecord.BackgroundJobId))
        {
            _ = jobClient.Delete(mangaDownloadRecord.BackgroundJobId);
        }

        List<ChapterDownloadRecord> chapterDownloads = libDbContext.ChapterDownloadRecords
                                                                   .Query()
                                                                   .Where(p => (int)(object)p.DownloadStatus == (int)(object)DownloadStatus.Scheduled
                                                                            || (int)(object)p.DownloadStatus == (int)(object)DownloadStatus.ToBeRescheduled)
                                                                   .ToList();

        foreach (ChapterDownloadRecord chapterDownload in chapterDownloads)
        {
            CancelChapterDownload(chapterDownload);
        }

        RemoveDiscoverRecurringJob(mangaDownloadRecord.Library);
    }

    public void CancelChapterDownload(ChapterDownloadRecord chapterDownloadRecord)
    {
        if (!string.IsNullOrWhiteSpace(chapterDownloadRecord.BackgroundJobId))
        {
            _ = jobClient.Delete(chapterDownloadRecord.BackgroundJobId);
        }
    }

    /// <inheritdoc/>
    public void RemoveDiscoverRecurringJob(Library library)
    {
        RecurringJob.RemoveIfExists(library.GetDiscovertyJobId());
    }

    /// <inheritdoc/>
    public void ScheduleDiscoverRecurringJob(Library library, TimeSpan? schedule)
    {
        string mangaDiscoveryQueue = workerOptions.Value.DiscoveryNewChapterQueues.First();

        string cronExpression = schedule.HasValue ? schedule.Value.ToCronDailyExpression() : workerOptions.Value.DailyExecutionTime.ToCronDailyExpression();
        RecurringJob.AddOrUpdate<IChapterDiscoveryJob>(library.GetDiscovertyJobId(), (job) => job.DispatchAsync(mangaDiscoveryQueue, library.CrawlerAgent.Id, library.Id, null!, CancellationToken.None), cronExpression, new RecurringJobOptions()
        {
            TimeZone = TimeZoneInfo.Local,
        });
    }

    /// <inheritdoc/>
    public string TriggerDiscoverRecurringJob(Library library)
    {
        string mangaDiscoveryQueue = workerOptions.Value.DiscoveryNewChapterQueues.First();

        string jobId = BackgroundJob.Enqueue<IChapterDiscoveryJob>((job) => job.DispatchAsync(mangaDiscoveryQueue, library.CrawlerAgent.Id, library.Id, null!, CancellationToken.None));

        using IStorageConnection connection = JobStorage.Current.GetConnection();

        connection.SetJobParameter(jobId, Defaults.Worker.RecurringJobId, library.GetDiscovertyJobId());
        connection.SetJobParameter(jobId, Defaults.Worker.LibraryId, library.Id.ToString());
        connection.SetJobParameter(jobId, Defaults.Worker.CrawlerAgentId, library.CrawlerAgent.Id.ToString());

        return jobId;
    }

    /// <inheritdoc/>
    public bool IsDiscoverRecurringJobScheduled(Library library)
    {
        using IStorageConnection connection = JobStorage.Current.GetConnection();

        List<RecurringJobDto> recurringJobs = connection.GetRecurringJobs();

        return recurringJobs.Any(job => string.Equals(job.Id, library.GetDiscovertyJobId(), StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public bool IsDiscoverRecurringJobRunning(Library library)
    {
        using IStorageConnection connection = JobStorage.Current.GetConnection();

        IMonitoringApi monitoring = JobStorage.Current.GetMonitoringApi();
        bool exists =
            monitoring.ProcessingJobs(0, 200)
                .Any(j => string.Equals(connection.GetJobParameter(j.Key, Defaults.Worker.RecurringJobId), library.GetDiscovertyJobId(), StringComparison.InvariantCulture))
            ||
            monitoring.ScheduledJobs(0, 200)
                .Any(j => string.Equals(connection.GetJobParameter(j.Key, Defaults.Worker.RecurringJobId), library.GetDiscovertyJobId(), StringComparison.InvariantCulture));
        return exists;
    }
    /// <inheritdoc/>
    public void CancelJobsForCrawlerAgent(CrawlerAgent crawlerAgent)
    {
        using IStorageConnection connection = JobStorage.Current.GetConnection();
        IMonitoringApi monitoring = JobStorage.Current.GetMonitoringApi();

        string crawlerAgentId = crawlerAgent.Id.ToString();

        bool MatchesAgent(string jobId)
        {
            string jobCrawlerAgentId = connection.GetJobParameter(jobId, Defaults.Worker.CrawlerAgentId);
            return string.Equals(
                jobCrawlerAgentId,
                crawlerAgentId,
                StringComparison.OrdinalIgnoreCase);
        }

        HashSet<string> jobIds = [];

        // Processing
        for (int page = 0; ; page++)
        {
            JobList<ProcessingJobDto> jobs = monitoring.ProcessingJobs(page * 100, 100);

            if (jobs.Count == 0)
            {
                break;
            }

            foreach (KeyValuePair<string, ProcessingJobDto> job in jobs)
            {
                if (MatchesAgent(job.Key))
                {
                    _ = jobIds.Add(job.Key);
                }
            }
        }

        // Scheduled
        for (int page = 0; ; page++)
        {
            JobList<ScheduledJobDto> jobs = monitoring.ScheduledJobs(page * 100, 100);

            if (jobs.Count == 0)
            {
                break;
            }

            foreach (KeyValuePair<string, ScheduledJobDto> job in jobs)
            {
                if (MatchesAgent(job.Key))
                {
                    _ = jobIds.Add(job.Key);
                }
            }
        }

        // Enqueued - all relevant queues
        foreach (string queue in workerOptions.Value.GetAllQueues().Where(p => !string.Equals(p, Defaults.Worker.DeferredExecutionQueue, StringComparison.OrdinalIgnoreCase)))
        {
            for (int page = 0; ; page++)
            {
                JobList<EnqueuedJobDto> jobs = monitoring.EnqueuedJobs(queue, page * 100, 100);

                if (jobs.Count == 0)
                {
                    break;
                }

                foreach (KeyValuePair<string, EnqueuedJobDto> job in jobs)
                {
                    if (job.Value.State != "Deleted" && MatchesAgent(job.Key))
                    {
                        _ = jobIds.Add(job.Key);
                    }
                }
            }
        }

        // Delete everything we found.
        foreach (string jobId in jobIds)
        {
            try
            {
                _ = jobClient.Delete(jobId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete job {JobId} for crawler agent {CrawlerAgentId}", jobId, crawlerAgent.Id);
            }
        }
    }


}
