using KamiYomu.CrawlerAgents.Core.Catalog;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Libraries.ViewComponents;

public class MangaInfoVerticalViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(Manga manga)
    {
        return View(new MangaInfoViewVerticalComponentModel(manga));
    }
}

public record MangaInfoViewVerticalComponentModel(Manga? Manga);

