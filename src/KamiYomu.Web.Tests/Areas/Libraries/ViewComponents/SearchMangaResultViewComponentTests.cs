using KamiYomu.Web.Areas.Libraries.ViewComponents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class SearchMangaResultViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithLibrariesAndSearchUri()
    {
        List<Library> libraries = [ServiceTestHelpers.CreateLibrary("Alpha"), ServiceTestHelpers.CreateLibrary("Beta")];
        SearchMangaResultViewComponent component = new();

        IViewComponentResult result = component.Invoke(libraries, "/Libraries/Collection/Index?query=alpha");

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        SearchMangaResultViewComponentModel model = Assert.IsType<SearchMangaResultViewComponentModel>(viewResult.ViewData.Model);
        Assert.Same(libraries, model.Libraries);
        Assert.Equal("/Libraries/Collection/Index?query=alpha", model.SearchUri);
    }

    [Fact]
    public void Invoke_WithEmptyLibraries_ReturnsModelWithEmptyCollection()
    {
        List<Library> libraries = [];
        SearchMangaResultViewComponent component = new();

        IViewComponentResult result = component.Invoke(libraries, "/search");

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        SearchMangaResultViewComponentModel model = Assert.IsType<SearchMangaResultViewComponentModel>(viewResult.ViewData.Model);
        Assert.Empty(model.Libraries);
    }
}
