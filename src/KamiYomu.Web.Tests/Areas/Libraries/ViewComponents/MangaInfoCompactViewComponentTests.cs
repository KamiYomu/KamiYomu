using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Areas.Libraries.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class MangaInfoCompactViewComponentTests
{
    [Fact]
    public void Invoke_ReturnsViewWithMangaModel()
    {
        Manga manga = MangaBuilder.Create().WithTitle("Alpha").Build();
        MangaInfoCompactViewComponent component = new();

        IViewComponentResult result = component.Invoke(manga);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaInfoViewCompactComponentModel model = Assert.IsType<MangaInfoViewCompactComponentModel>(viewResult.ViewData.Model);
        Assert.Same(manga, model.Manga);
    }

    [Fact]
    public void Invoke_WithNullManga_ReturnsModelWithNullManga()
    {
        MangaInfoCompactViewComponent component = new();

        IViewComponentResult result = component.Invoke(null);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaInfoViewCompactComponentModel model = Assert.IsType<MangaInfoViewCompactComponentModel>(viewResult.ViewData.Model);
        Assert.Null(model.Manga);
    }
}
