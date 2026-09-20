using KamiYomu.Web.Areas.Reader.ViewComponents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Reader.ViewComponents;

public class MangaGridItemViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithProvidedLibrary()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        MangaGridItemViewComponent component = new();

        IViewComponentResult result = component.Invoke(library);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        Assert.Same(library, viewResult.ViewData.Model);
    }
}
