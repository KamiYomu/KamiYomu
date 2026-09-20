using KamiYomu.Web.AppOptions;

namespace KamiYomu.Web.Tests.AppOptions;

public class WorkerOptionsTests
{
    [Fact]
    public void GetAllQueues_ReturnsBuiltInAndConfiguredQueuesInOrder()
    {
        // Arrange
        WorkerOptions options = new()
        {
            ServerAvailableNames = ["server-a"],
            DownloadChapterQueues = ["chapter-1", "chapter-2"],
            MangaDownloadSchedulerQueues = ["scheduler-1"],
            DiscoveryNewChapterQueues = ["discovery-1", "discovery-2"],
        };

        // Act
        string[] result = options.GetAllQueues().ToArray();

        // Assert
        Assert.Equal(
        [
            Defaults.Worker.DefaultQueue,
            Defaults.Worker.DeferredExecutionQueue,
            Defaults.Worker.NotificationQueue,
            "chapter-1",
            "chapter-2",
            "scheduler-1",
            "discovery-1",
            "discovery-2",
        ],
        result);
    }

    [Fact]
    public void GetWaitPeriod_ReturnsConfiguredMinimum_WhenMaximumIsExclusiveUpperBound()
    {
        // Arrange
        WorkerOptions options = new()
        {
            ServerAvailableNames = ["server-a"],
            DownloadChapterQueues = ["chapter-1"],
            MangaDownloadSchedulerQueues = ["scheduler-1"],
            DiscoveryNewChapterQueues = ["discovery-1"],
            MinWaitPeriodInMilliseconds = 3000,
            MaxWaitPeriodInMilliseconds = 3001,
        };

        // Act
        TimeSpan result = options.GetWaitPeriod();

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(3000), result);
    }
}
