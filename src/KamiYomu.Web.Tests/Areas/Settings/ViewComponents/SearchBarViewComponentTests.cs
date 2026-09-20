using KamiYomu.Web.Areas.Settings.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Settings.ViewComponents;

public class SearchBarViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedModel()
    {
        SearchBarViewModel viewModel = new() { Search = "term" };
        SearchBarViewComponent component = new();

        IViewComponentResult result = component.Invoke(viewModel);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Same(viewModel, viewResult.ViewData.Model);
    }

    [Fact]
    public void SearchBarViewModel_HasExpectedDefaults()
    {
        SearchBarViewModel viewModel = new();

        Assert.Equal(string.Empty, viewModel.Search);
        Assert.False(viewModel.IncludePrerelease);
        Assert.Equal(Guid.Empty, viewModel.SourceId);
        Assert.Empty(viewModel.Sources);
    }
}
