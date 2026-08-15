using KamiYomu.CrawlerAgents.Core.Catalog;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Libraries.ViewComponents;

public class MangaInfoCompactViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(Manga manga)
    {
        return View(new MangaInfoViewCompactComponentModel(manga));
    }
}

public record MangaInfoViewCompactComponentModel(Manga? Manga);
