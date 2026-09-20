using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Repositories;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Infrastructure.Repositories;

[Collection(KamiYomu.Web.Tests.Infrastructure.AppServices.SharedInfrastructureStateCollection.Name)]
public class HangfireRepositoryTests
{
    [Fact]
    public void GetLeastLoadedDownloadChapterQueue_ReturnsConfiguredQueueWithLowestObservedLoad()
    {
        Mock<IMonitoringApi> monitoringApi = new();
        _ = monitoringApi
            .Setup(api => api.Queues())
            .Returns(
            [
                new QueueWithTopEnqueuedJobsDto { Name = "chapter-queue-a", Length = 3 },
                new QueueWithTopEnqueuedJobsDto { Name = "chapter-queue-b", Length = 1 }
            ]);

        Mock<JobStorage> jobStorage = new();
        _ = jobStorage.Setup(storage => storage.GetMonitoringApi()).Returns(monitoringApi.Object);

        JobStorage.Current = jobStorage.Object;

        HangfireRepository repository = new(Options.Create(new WorkerOptions
        {
            ServerAvailableNames = ["server-1"],
            DownloadChapterQueues = ["chapter-queue-a", "chapter-queue-b", "chapter-queue-c"],
            MangaDownloadSchedulerQueues = ["manga-queue-a"],
            DiscoveryNewChapterQueues = ["discovery-queue-a"]
        }));

        EnqueuedState result = repository.GetLeastLoadedDownloadChapterQueue();

        Assert.Equal("chapter-queue-c", result.Queue);
    }

    [Fact]
    public void GetLeastLoadedMangaDownloadSchedulerQueue_ReturnsLeastLoadedConfiguredQueue()
    {
        Mock<IMonitoringApi> monitoringApi = new();
        _ = monitoringApi
            .Setup(api => api.Queues())
            .Returns(
            [
                new QueueWithTopEnqueuedJobsDto { Name = "manga-queue-a", Length = 5 },
                new QueueWithTopEnqueuedJobsDto { Name = "manga-queue-b", Length = 2 }
            ]);

        Mock<JobStorage> jobStorage = new();
        _ = jobStorage.Setup(storage => storage.GetMonitoringApi()).Returns(monitoringApi.Object);

        JobStorage.Current = jobStorage.Object;

        HangfireRepository repository = new(Options.Create(new WorkerOptions
        {
            ServerAvailableNames = ["server-1"],
            DownloadChapterQueues = ["chapter-queue-a"],
            MangaDownloadSchedulerQueues = ["manga-queue-a", "manga-queue-b"],
            DiscoveryNewChapterQueues = ["discovery-queue-a"]
        }));

        EnqueuedState result = repository.GetLeastLoadedMangaDownloadSchedulerQueue();

        Assert.Equal("manga-queue-b", result.Queue);
    }

    [Fact]
    public void GetNotifyQueue_ReturnsNotificationQueue()
    {
        HangfireRepository repository = new(Options.Create(new WorkerOptions
        {
            ServerAvailableNames = ["server-1"],
            DownloadChapterQueues = ["chapter-queue-a"],
            MangaDownloadSchedulerQueues = ["manga-queue-a"],
            DiscoveryNewChapterQueues = ["discovery-queue-a"]
        }));

        EnqueuedState result = repository.GetNotifyQueue();

        Assert.Equal(Defaults.Worker.NotificationQueue, result.Queue);
    }
}
