using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

using KamiYomu.CrawlerAgents.Core;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class StatsServiceTests
{
    [Fact]
    public void GetStats_ReturnsCollectionAndWorkerCounts()
    {
        ServiceTestHelpers.InitializeCache(nameof(StatsServiceTests));

        using DbContext dbContext = new(":memory:");
        _ = dbContext.Libraries.Insert(ServiceTestHelpers.CreateLibrary("One"));
        _ = dbContext.Libraries.Insert(ServiceTestHelpers.CreateLibrary("Two"));

        Mock<IMonitoringApi> monitoringApi = new();
        _ = monitoringApi.Setup(x => x.Servers()).Returns(
        [
            new ServerDto { Name = "server-1" },
            new ServerDto { Name = "server-2" }
        ]);
        _ = monitoringApi.Setup(x => x.Queues()).Returns(
        [
            new QueueWithTopEnqueuedJobsDto { Name = "default", Length = 3 },
            new QueueWithTopEnqueuedJobsDto { Name = "downloads", Length = 4 }
        ]);
        _ = monitoringApi.Setup(x => x.FailedCount()).Returns(5);

        Mock<IStorageConnection> connection = new();
        Mock<JobStorage> jobStorage = new();
        _ = jobStorage.Setup(x => x.GetMonitoringApi()).Returns(monitoringApi.Object);
        _ = jobStorage.Setup(x => x.GetConnection()).Returns(connection.Object);

        StatsService service = new(dbContext, new CacheContext(), jobStorage.Object);

        var result = service.GetStats();

        Assert.Equal(2, result.CollectionSize);
        Assert.Equal(7, result.WorkerQueuedTasks);
        Assert.Equal(5, result.WorkerFailedTasks);
        Assert.NotNull(result.Version);
        Assert.NotNull(result.CoreVersion);
    }
}
