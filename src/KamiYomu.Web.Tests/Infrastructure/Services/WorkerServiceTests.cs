using Hangfire;
using Hangfire.States;
using Hangfire.Storage.SQLite;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class WorkerServiceTests
{
    [Fact]
    public void ScheduleMangaDownload_SetsJobParameters_AndSchedulesRecurringDiscovery()
    {
        string storagePath = CreateStoragePath();
        JobStorage.Current = new SQLiteStorage(storagePath);
        _ = GlobalConfiguration.Configuration.UseStorage(JobStorage.Current);

        try
        {
            Mock<IHangfireRepository> repository = new();
            _ = repository.Setup(x => x.GetLeastLoadedMangaDownloadSchedulerQueue()).Returns(new Hangfire.States.EnqueuedState("manga-queue"));

            WorkerService service = CreateService(repository.Object, new BackgroundJobClient(JobStorage.Current));
            Library library = ServiceTestHelpers.CreateLibrary();
            MangaDownloadRecord mangaDownload = new(library, "parent-job");

            string jobId = service.ScheduleMangaDownload(mangaDownload, TimeSpan.FromHours(6));

            using var connection = JobStorage.Current.GetConnection();
            Assert.False(string.IsNullOrWhiteSpace(jobId));
            Assert.Equal(library.Id.ToString(), connection.GetJobParameter(jobId, Defaults.Worker.LibraryId));
            Assert.Equal(library.CrawlerAgent.Id.ToString(), connection.GetJobParameter(jobId, Defaults.Worker.CrawlerAgentId));
            Assert.Equal(TimeSpan.FromHours(6), service.GetDiscoverySchedule(library));
        }
        finally
        {
            CleanupStorage(storagePath);
        }
    }

    [Fact]
    public void TriggerDiscoverRecurringJob_SetsRecurringJobParameters()
    {
        string storagePath = CreateStoragePath();
        JobStorage.Current = new SQLiteStorage(storagePath);
        _ = GlobalConfiguration.Configuration.UseStorage(JobStorage.Current);

        try
        {
            WorkerService service = CreateService(Mock.Of<IHangfireRepository>(), new BackgroundJobClient(JobStorage.Current));
            Library library = ServiceTestHelpers.CreateLibrary();

            string jobId = service.TriggerDiscoverRecurringJob(library);

            using var connection = JobStorage.Current.GetConnection();
            Assert.Equal(library.GetDiscovertyJobId(), connection.GetJobParameter(jobId, Defaults.Worker.RecurringJobId));
            Assert.Equal(library.Id.ToString(), connection.GetJobParameter(jobId, Defaults.Worker.LibraryId));
            Assert.Equal(library.CrawlerAgent.Id.ToString(), connection.GetJobParameter(jobId, Defaults.Worker.CrawlerAgentId));
        }
        finally
        {
            CleanupStorage(storagePath);
        }
    }

    [Fact]
    public void IsDiscoverRecurringJobScheduled_ReturnsTrue_WhenRecurringJobExists()
    {
        string storagePath = CreateStoragePath();
        JobStorage.Current = new SQLiteStorage(storagePath);
        _ = GlobalConfiguration.Configuration.UseStorage(JobStorage.Current);

        try
        {
            WorkerService service = CreateService(Mock.Of<IHangfireRepository>(), new BackgroundJobClient(JobStorage.Current));
            Library library = ServiceTestHelpers.CreateLibrary();

            service.ScheduleDiscoverRecurringJob(library, TimeSpan.FromHours(5));

            Assert.True(service.IsDiscoverRecurringJobScheduled(library));
        }
        finally
        {
            CleanupStorage(storagePath);
        }
    }

    [Fact]
    public void IsDiscoverRecurringJobRunning_ReturnsTrue_WhenScheduledJobMatchesRecurringJobId()
    {
        string storagePath = CreateStoragePath();
        JobStorage.Current = new SQLiteStorage(storagePath);
        _ = GlobalConfiguration.Configuration.UseStorage(JobStorage.Current);

        try
        {
            WorkerService service = CreateService(Mock.Of<IHangfireRepository>(), new BackgroundJobClient(JobStorage.Current));
            Library library = ServiceTestHelpers.CreateLibrary();

            string jobId = BackgroundJob.Schedule<IChapterDiscoveryJob>(
                job => job.DispatchAsync("discovery", library.CrawlerAgent.Id, library.Id, null!, CancellationToken.None),
                TimeSpan.FromMinutes(5));

            using (var connection = JobStorage.Current.GetConnection())
            {
                connection.SetJobParameter(jobId, Defaults.Worker.RecurringJobId, library.GetDiscovertyJobId());
            }

            Assert.True(service.IsDiscoverRecurringJobRunning(library));
        }
        finally
        {
            CleanupStorage(storagePath);
        }
    }

    [Fact]
    public void CancelChapterDownload_DeletesBackgroundJob_WhenPresent()
    {
        Mock<IBackgroundJobClient> jobClient = new();
        WorkerService service = CreateService(Mock.Of<IHangfireRepository>(), jobClient.Object);
        Library library = ServiceTestHelpers.CreateLibrary();
        MangaDownloadRecord mangaDownload = new(library, "parent");
        ChapterDownloadRecord chapterDownload = new(library.CrawlerAgent, mangaDownload, ServiceTestHelpers.CreateChapter());
        chapterDownload.Scheduled("chapter-job");

        service.CancelChapterDownload(chapterDownload);

        jobClient.Verify(x => x.ChangeState("chapter-job", It.IsAny<DeletedState>(), null), Times.Once);
    }

    [Fact]
    public void CancelMangaDownload_DeletesParentAndScheduledChildJobs_AndRemovesRecurringJob()
    {
        string storagePath = CreateStoragePath();
        JobStorage.Current = new SQLiteStorage(storagePath);
        _ = GlobalConfiguration.Configuration.UseStorage(JobStorage.Current);

        Library library = ServiceTestHelpers.CreateLibrary();
        MangaDownloadRecord mangaDownload = new(library, "parent-job");

        ChapterDownloadRecord scheduledChapter = new(library.CrawlerAgent, mangaDownload, ServiceTestHelpers.CreateChapter(1, "One"));
        ServiceTestHelpers.AssignId(scheduledChapter);
        scheduledChapter.Scheduled("chapter-job");

        ChapterDownloadRecord pendingChapter = new(library.CrawlerAgent, mangaDownload, ServiceTestHelpers.CreateChapter(2, "Two"));
        ServiceTestHelpers.AssignId(pendingChapter);

        using LibraryDbContext libraryDbContext = library.GetReadWriteDbContext();
        _ = libraryDbContext.ChapterDownloadRecords.Insert(scheduledChapter);
        _ = libraryDbContext.ChapterDownloadRecords.Insert(pendingChapter);

        Mock<IBackgroundJobClient> jobClient = new();
        WorkerService service = CreateService(Mock.Of<IHangfireRepository>(), jobClient.Object);
        service.ScheduleDiscoverRecurringJob(library, TimeSpan.FromHours(4));

        try
        {
            service.CancelMangaDownload(mangaDownload);

            jobClient.Verify(x => x.ChangeState("parent-job", It.IsAny<DeletedState>(), null), Times.Once);
            jobClient.Verify(x => x.ChangeState("chapter-job", It.IsAny<DeletedState>(), null), Times.Once);
            Assert.False(service.IsDiscoverRecurringJobScheduled(library));
        }
        finally
        {
            ServiceTestHelpers.CleanupLibraryArtifacts(library);
            CleanupStorage(storagePath);
        }
    }

    [Fact]
    public void CancelJobsForCrawlerAgent_DeletesMatchingEnqueuedAndScheduledJobs()
    {
        string storagePath = CreateStoragePath();
        JobStorage.Current = new SQLiteStorage(storagePath);
        _ = GlobalConfiguration.Configuration.UseStorage(JobStorage.Current);

        try
        {
            Library library = ServiceTestHelpers.CreateLibrary();
            Library otherLibrary = ServiceTestHelpers.CreateLibrary();
            Guid targetDownloadId = Guid.NewGuid();
            Guid targetScheduledDownloadId = Guid.NewGuid();
            Guid otherDownloadId = Guid.NewGuid();

            string enqueuedJob = BackgroundJob.Enqueue<IMangaDownloaderJob>(
                "manga-queue",
                job => job.DispatchAsync("manga-queue", library.CrawlerAgent.Id, library.Id, targetDownloadId, "Target", null!, CancellationToken.None));

            string scheduledJob = BackgroundJob.Schedule<IMangaDownloaderJob>(
                "manga-queue",
                job => job.DispatchAsync("manga-queue", library.CrawlerAgent.Id, library.Id, targetScheduledDownloadId, "Target", null!, CancellationToken.None),
                TimeSpan.FromMinutes(10));

            string otherJob = BackgroundJob.Enqueue<IMangaDownloaderJob>(
                "manga-queue",
                job => job.DispatchAsync("manga-queue", otherLibrary.CrawlerAgent.Id, otherLibrary.Id, otherDownloadId, "Other", null!, CancellationToken.None));

            using (var connection = JobStorage.Current.GetConnection())
            {
                connection.SetJobParameter(enqueuedJob, Defaults.Worker.CrawlerAgentId, library.CrawlerAgent.Id.ToString());
                connection.SetJobParameter(scheduledJob, Defaults.Worker.CrawlerAgentId, library.CrawlerAgent.Id.ToString());
                connection.SetJobParameter(otherJob, Defaults.Worker.CrawlerAgentId, otherLibrary.CrawlerAgent.Id.ToString());
            }

            Mock<IBackgroundJobClient> jobClient = new();
            WorkerService service = CreateService(Mock.Of<IHangfireRepository>(), jobClient.Object);

            service.CancelJobsForCrawlerAgent(library.CrawlerAgent);

            jobClient.Verify(x => x.ChangeState(enqueuedJob, It.IsAny<DeletedState>(), null), Times.Once);
            jobClient.Verify(x => x.ChangeState(scheduledJob, It.IsAny<DeletedState>(), null), Times.Once);
            jobClient.Verify(x => x.ChangeState(otherJob, It.IsAny<DeletedState>(), null), Times.Never);
        }
        finally
        {
            CleanupStorage(storagePath);
        }
    }

    private static WorkerService CreateService(IHangfireRepository repository, IBackgroundJobClient jobClient)
    {
        return new WorkerService(
            Mock.Of<ILogger<WorkerService>>(),
            Options.Create(new WorkerOptions
            {
                ServerAvailableNames = ["server-1"],
                DownloadChapterQueues = ["download-queue"],
                MangaDownloadSchedulerQueues = ["manga-queue"],
                DiscoveryNewChapterQueues = ["discovery"],
                DailyExecutionTime = TimeSpan.FromHours(2)
            }),
            repository,
            jobClient);
    }

    private static string CreateStoragePath()
    {
        _ = Directory.CreateDirectory(ServiceTestHelpers.ArtifactsRoot);
        return Path.Combine(ServiceTestHelpers.ArtifactsRoot, $"hangfire-{Guid.NewGuid():N}.sqlite");
    }

    private static void CleanupStorage(string storagePath)
    {
        foreach (string path in new[] { storagePath, $"{storagePath}-shm", $"{storagePath}-wal" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
