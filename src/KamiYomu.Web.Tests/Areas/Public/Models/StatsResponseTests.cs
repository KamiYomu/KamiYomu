using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class StatsResponseTests
{
    [Fact]
    public void Record_StoresConstructorValues()
    {
        StatsResponse response = new("1.2.3", "4.5.6", 7, 8, 9);

        Assert.Equal("1.2.3", response.Version);
        Assert.Equal("4.5.6", response.CoreVersion);
        Assert.Equal(7, response.CollectionSize);
        Assert.Equal(8, response.WorkerQueuedTasks);
        Assert.Equal(9, response.WorkerFailedTasks);
    }

    [Fact]
    public void Records_WithSameValues_AreEqual()
    {
        StatsResponse first = new("1.0.0", "2.0.0", 1, 2, 3);
        StatsResponse second = new("1.0.0", "2.0.0", 1, 2, 3);

        Assert.Equal(first, second);
    }
}
