using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Areas.Libraries.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class MangaInfoVerticalViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithMangaModel()
    {
        Manga manga = MangaBuilder.Create().WithTitle("Beta").Build();
        MangaInfoVerticalViewComponent component = new();

        IViewComponentResult result = component.Invoke(manga);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaInfoViewVerticalComponentModel model = Assert.IsType<MangaInfoViewVerticalComponentModel>(viewResult.ViewData.Model);
        Assert.Same(manga, model.Manga);
    }

    [Fact]
    public void Invoke_WithNullManga_ReturnsModelWithNullManga()
    {
        MangaInfoVerticalViewComponent component = new();

        IViewComponentResult result = component.Invoke(null);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaInfoViewVerticalComponentModel model = Assert.IsType<MangaInfoViewVerticalComponentModel>(viewResult.ViewData.Model);
        Assert.Null(model.Manga);
    }
}
