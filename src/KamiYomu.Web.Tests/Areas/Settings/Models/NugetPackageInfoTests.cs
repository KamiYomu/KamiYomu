using System.Text.Json.Nodes;

using KamiYomu.Web.Areas.Settings.Models;

namespace KamiYomu.Web.Tests.Areas.Settings.Models;

public class NugetPackageInfoTests
{
    [Fact]
    public void GetIconUri_ReturnsIconUrl_WhenPresent()
    {
        JsonNode info = new JsonObject
        {
            ["iconUrl"] = "https://example.com/icon.png"
        };

        Uri result = NugetPackageInfo.GetIconUri(info, "MyPackage");

        Assert.Equal(new Uri("https://example.com/icon.png"), result);
    }

    [Fact]
    public void GetIconUri_FallsBackToProjectUrl_WhenIconUrlMissing()
    {
        JsonNode info = new JsonObject
        {
            ["projectUrl"] = "https://example.com/repo"
        };

        Uri result = NugetPackageInfo.GetIconUri(info, "MyPackage");

        Assert.Equal(new Uri("https://example.com/repo/raw/branch/main/src/MyPackage/Resources/logo.png"), result);
    }

    [Fact]
    public void GetIconUri_TrimsTrailingSlash_FromProjectUrl()
    {
        JsonNode info = new JsonObject
        {
            ["projectUrl"] = "https://example.com/repo/"
        };

        Uri result = NugetPackageInfo.GetIconUri(info, "MyPackage");

        Assert.Equal(new Uri("https://example.com/repo/raw/branch/main/src/MyPackage/Resources/logo.png"), result);
    }

    [Fact]
    public void GetIconUri_ReturnsRelativeFavicon_WhenBothMissing()
    {
        JsonNode info = new JsonObject();

        Uri result = NugetPackageInfo.GetIconUri(info, "MyPackage");

        Assert.False(result.IsAbsoluteUri);
        Assert.Equal("/images/favicon.ico", result.OriginalString);
    }

    [Fact]
    public void GetIconUri_ReturnsRelativeFavicon_WhenInfoIsNull()
    {
        Uri result = NugetPackageInfo.GetIconUri(null, "MyPackage");

        Assert.False(result.IsAbsoluteUri);
        Assert.Equal("/images/favicon.ico", result.OriginalString);
    }

    [Fact]
    public void GetNugetPackageKamiYomuCoreRangeVersion_ReturnsRange_WhenDependencyPresent()
    {
        NugetPackageInfo package = new()
        {
            Dependencies = ["KamiYomu.CrawlerAgents.Core:[1.2.0, )", "Other.Package:1.0.0"]
        };

        string result = package.GetNugetPackageKamiYomuCoreRangeVersion();

        Assert.Equal("[1.2.0, )", result);
    }

    [Fact]
    public void GetNugetPackageKamiYomuCoreRangeVersion_ReturnsUnknown_WhenNoDependencies()
    {
        NugetPackageInfo package = new();

        Assert.Equal("Unknown", package.GetNugetPackageKamiYomuCoreRangeVersion());
    }

    [Fact]
    public void GetNugetPackageKamiYomuCoreRangeVersion_ReturnsUnknown_WhenDependencyNotFound()
    {
        NugetPackageInfo package = new()
        {
            Dependencies = ["Other.Package:1.0.0"]
        };

        Assert.Equal("Unknown", package.GetNugetPackageKamiYomuCoreRangeVersion());
    }

    [Fact]
    public void GetNugetPackageKamiYomuCoreRangeVersion_ReturnsUnknown_WhenMalformed()
    {
        NugetPackageInfo package = new()
        {
            Dependencies = ["KamiYomu.CrawlerAgents.Core"]
        };

        Assert.Equal("Unknown", package.GetNugetPackageKamiYomuCoreRangeVersion());
    }

    [Fact]
    public void GetKamiYomuCoreVersion_ReturnsUnknown_WhenRangeIsUnknown()
    {
        NugetPackageInfo package = new();

        Assert.Equal("Unknown", package.GetKamiYomuCoreVersion());
    }

    [Fact]
    public void GetKamiYomuCoreVersion_StripsBracketsAndReturnsFirstPart()
    {
        NugetPackageInfo package = new()
        {
            Dependencies = ["KamiYomu.CrawlerAgents.Core:[1.2.0, 2.0.0)"]
        };

        Assert.Equal("1.2.0", package.GetKamiYomuCoreVersion());
    }

    [Fact]
    public void GetKamiYomuCoreVersion_StripsParenthesesAndReturnsFirstPart()
    {
        NugetPackageInfo package = new()
        {
            Dependencies = ["KamiYomu.CrawlerAgents.Core:(1.2.0, 2.0.0)"]
        };

        Assert.Equal("1.2.0", package.GetKamiYomuCoreVersion());
    }

    [Fact]
    public void IsVersionCompatible_ReturnsFalse_WhenNoDependencyFound()
    {
        NugetPackageInfo package = new();

        Assert.False(package.IsVersionCompatible());
    }

    [Fact]
    public void IsVersionCompatible_ReturnsFalse_WhenRangeIsMalformed()
    {
        NugetPackageInfo package = new()
        {
            Dependencies = ["KamiYomu.CrawlerAgents.Core:not-a-version-range"]
        };

        Assert.False(package.IsVersionCompatible());
    }

    [Fact]
    public void IsVersionCompatible_ReturnsFalse_WhenBelowMinimumRequiredVersion()
    {
        NugetPackageInfo package = new()
        {
            Dependencies = ["KamiYomu.CrawlerAgents.Core:[1.0.0, )"]
        };

        Assert.False(package.IsVersionCompatible());
    }

    [Fact]
    public void IsVersionCompatible_ReturnsTrue_ForValidCompatibleRange()
    {
        // The web project references KamiYomu.CrawlerAgents.Core 1.2.3 (see KamiYomu.Web.csproj),
        // which is >= the 1.1.4 minimum and satisfies an open-ended [1.1.4, ) range.
        NugetPackageInfo package = new()
        {
            Dependencies = ["KamiYomu.CrawlerAgents.Core:[1.1.4, )"]
        };

        Assert.True(package.IsVersionCompatible());
    }

    [Fact]
    public void IsNsfw_ReturnsTrue_WhenTagMatchesExactly()
    {
        NugetPackageInfo package = new() { Tags = ["nsfw"] };

        Assert.True(package.IsNsfw());
    }

    [Fact]
    public void IsNsfw_ReturnsTrue_WhenTagMatchesCaseInsensitively()
    {
        NugetPackageInfo package = new() { Tags = ["NSFW"] };

        Assert.True(package.IsNsfw());
    }

    [Fact]
    public void IsNsfw_ReturnsFalse_WhenNoTagMatches()
    {
        NugetPackageInfo package = new() { Tags = ["manga", "action"] };

        Assert.False(package.IsNsfw());
    }

    [Fact]
    public void IsNsfw_ReturnsFalse_WhenTagsEmpty()
    {
        NugetPackageInfo package = new() { Tags = [] };

        Assert.False(package.IsNsfw());
    }
}
