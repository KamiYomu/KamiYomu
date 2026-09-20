using KamiYomu.Web.Areas.Settings.Models;

namespace KamiYomu.Web.Tests.Areas.Settings.Models;

public class NugetDependencyInfoTests
{
    [Fact]
    public void Construction_SetsProvidedValues()
    {
        NugetDependencyInfo info = new()
        {
            Id = "KamiYomu.CrawlerAgents.Core",
            TargetFramework = "net8.0",
            VersionRange = "[1.2.0, )"
        };

        Assert.Equal("KamiYomu.CrawlerAgents.Core", info.Id);
        Assert.Equal("net8.0", info.TargetFramework);
        Assert.Equal("[1.2.0, )", info.VersionRange);
    }

    [Fact]
    public void Construction_DefaultsToNullValues()
    {
        NugetDependencyInfo info = new();

        Assert.Null(info.Id);
        Assert.Null(info.TargetFramework);
        Assert.Null(info.VersionRange);
    }
}
