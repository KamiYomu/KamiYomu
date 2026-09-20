using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Settings.ViewComponents;

public class PackageListViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedModel()
    {
        PackageListViewModel viewModel = new() { SourceId = Guid.NewGuid() };
        PackageListViewComponent component = new();

        IViewComponentResult result = component.Invoke(viewModel);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Same(viewModel, viewResult.ViewData.Model);
    }

    [Fact]
    public void PackageListViewModel_DefaultsToEmptyPackageItems()
    {
        PackageListViewModel viewModel = new();

        Assert.Empty(viewModel.PackageItems);
        Assert.Equal(Guid.Empty, viewModel.SourceId);
    }
}
