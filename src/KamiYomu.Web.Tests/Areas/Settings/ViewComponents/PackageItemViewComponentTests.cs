using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Settings.ViewComponents;

public class PackageItemViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedModel()
    {
        NugetPackageGroupedViewModel viewModel = new()
        {
            Id = "My.Package",
            VersionSelected = new NugetPackageInfo()
        };
        PackageItemViewComponent component = new();

        IViewComponentResult result = component.Invoke(viewModel);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Same(viewModel, viewResult.ViewData.Model);
    }
}

public class NugetPackageGroupedViewModelTests
{
    [Fact]
    public void GetCardId_ReplacesDotsWithHyphens()
    {
        NugetPackageGroupedViewModel viewModel = new()
        {
            Id = "My.Package.Name",
            VersionSelected = new NugetPackageInfo()
        };

        Assert.Equal("package-card-My-Package-Name", viewModel.GetCardId());
    }

    [Fact]
    public void GetCardId_ReturnsUnchangedPrefix_WhenNoDotsInId()
    {
        NugetPackageGroupedViewModel viewModel = new()
        {
            Id = "MyPackage",
            VersionSelected = new NugetPackageInfo()
        };

        Assert.Equal("package-card-MyPackage", viewModel.GetCardId());
    }

    [Fact]
    public void IsNsfw_ReturnsTrue_WhenTagPresentCaseInsensitively()
    {
        NugetPackageGroupedViewModel viewModel = new()
        {
            Id = "My.Package",
            Tags = ["NSFW"],
            VersionSelected = new NugetPackageInfo()
        };

        Assert.True(viewModel.IsNsfw());
    }

    [Fact]
    public void IsNsfw_ReturnsFalse_WhenTagAbsent()
    {
        NugetPackageGroupedViewModel viewModel = new()
        {
            Id = "My.Package",
            Tags = ["manga"],
            VersionSelected = new NugetPackageInfo()
        };

        Assert.False(viewModel.IsNsfw());
    }
}
