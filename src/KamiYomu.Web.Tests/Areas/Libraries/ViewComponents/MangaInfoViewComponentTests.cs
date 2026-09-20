using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Areas.Libraries.ViewComponents;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class MangaInfoViewComponentTests
{
    [Fact]
    public void Invoke_WithShortDescription_DoesNotNeedExpandAndPreviewMatchesDescription()
    {
        Manga manga = MangaBuilder.Create().WithTitle("Gamma").WithDescription("A short description.").Build();
        MangaInfoViewComponent component = new();

        IViewComponentResult result = component.Invoke(manga);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaInfoViewComponentModel model = Assert.IsType<MangaInfoViewComponentModel>(viewResult.ViewData.Model);
        Assert.Same(manga, model.Manga);
        Assert.Equal(manga.Description, model.Preview);
        Assert.False(model.NeedsExpand);
    }

    [Fact]
    public void Invoke_WithLongDescription_TruncatesPreviewAndNeedsExpand()
    {
        string longDescription = new('x', 120);
        Manga manga = MangaBuilder.Create().WithTitle("Delta").WithDescription(longDescription).Build();
        MangaInfoViewComponent component = new();

        IViewComponentResult result = component.Invoke(manga);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaInfoViewComponentModel model = Assert.IsType<MangaInfoViewComponentModel>(viewResult.ViewData.Model);
        Assert.Equal(longDescription[..50] + "...", model.Preview);
        Assert.True(model.NeedsExpand);
    }

    [Fact]
    public void Invoke_WithNoDescription_PreviewIsNullAndNeedsExpandIsFalse()
    {
        Manga manga = MangaBuilder.Create().WithTitle("Epsilon").Build();
        MangaInfoViewComponent component = new();

        IViewComponentResult result = component.Invoke(manga);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        MangaInfoViewComponentModel model = Assert.IsType<MangaInfoViewComponentModel>(viewResult.ViewData.Model);
        Assert.Null(model.Preview);
        Assert.False(model.NeedsExpand);
    }
}
