using KamiYomu.Web.Areas.Settings.Models;

namespace KamiYomu.Web.Tests.Areas.Settings.Models;

public class PastJobInfoTests
{
    [Fact]
    public void Construction_SetsAllProperties()
    {
        DateTime time = new(2024, 1, 1);
        PastJobInfo info = new("job-1", time, "Succeeded", "Enqueue", "Recurring");

        Assert.Equal("job-1", info.JobId);
        Assert.Equal(time, info.Time);
        Assert.Equal("Succeeded", info.State);
        Assert.Equal("Enqueue", info.Method);
        Assert.Equal("Recurring", info.Type);
    }

    [Fact]
    public void Construction_AllowsNullTime()
    {
        PastJobInfo info = new("job-2", null, "Scheduled", "Enqueue", "Once");

        Assert.Null(info.Time);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        DateTime time = new(2024, 1, 1);
        PastJobInfo first = new("job-1", time, "Succeeded", "Enqueue", "Recurring");
        PastJobInfo second = new("job-1", time, "Succeeded", "Enqueue", "Recurring");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Equality_DiffersWhenAnyPropertyDiffers()
    {
        DateTime time = new(2024, 1, 1);
        PastJobInfo first = new("job-1", time, "Succeeded", "Enqueue", "Recurring");
        PastJobInfo second = new("job-1", time, "Failed", "Enqueue", "Recurring");

        Assert.NotEqual(first, second);
    }
}
