using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class LockManagerTests
{
    [Fact]
    public void TryAcquireAsync_ReturnsNull_WhenAllSlotsAreInUse()
    {
        Mock<IHostEnvironment> hostEnvironment = new();
        LockManager manager = new(
            Options.Create(new WorkerOptions
            {
                ServerAvailableNames = ["server"],
                DownloadChapterQueues = ["download"],
                MangaDownloadSchedulerQueues = ["scheduler"],
                DiscoveryNewChapterQueues = ["discovery"],
                MaxConcurrentCrawlerInstances = 1
            }),
            hostEnvironment.Object);

        string crawlerId = Guid.NewGuid().ToString("N");

        using IDisposable? first = manager.TryAcquireAsync(crawlerId);
        IDisposable? second = manager.TryAcquireAsync(crawlerId);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void TryAcquireAsync_ReusesSlot_AfterHandleIsDisposed()
    {
        Mock<IHostEnvironment> hostEnvironment = new();
        LockManager manager = new(
            Options.Create(new WorkerOptions
            {
                ServerAvailableNames = ["server"],
                DownloadChapterQueues = ["download"],
                MangaDownloadSchedulerQueues = ["scheduler"],
                DiscoveryNewChapterQueues = ["discovery"],
                MaxConcurrentCrawlerInstances = 1
            }),
            hostEnvironment.Object);

        string crawlerId = Guid.NewGuid().ToString("N");

        IDisposable? first = manager.TryAcquireAsync(crawlerId);
        Assert.NotNull(first);

        first.Dispose();

        using IDisposable? second = manager.TryAcquireAsync(crawlerId);

        Assert.NotNull(second);
    }
}
