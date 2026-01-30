using KamiYomu.Web.Entities;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Libraries.ViewComponents;

public class SearchMangaResultViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(IEnumerable<Library> libraries, string searchUri)
    {
        return View(new SearchMangaResultViewComponentModel(libraries, searchUri));
    }
}

public record SearchMangaResultViewComponentModel(IEnumerable<Library> Libraries, string SearchUri);
