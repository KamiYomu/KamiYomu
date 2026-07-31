using KamiYomu.Web.Entities;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Libraries.ViewComponents;
/// <summary>
/// 
/// </summary>
public class SearchMangaResultViewComponent : ViewComponent
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="libraries"></param>
    /// <param name="searchUri"></param>
    /// <returns></returns>
    public IViewComponentResult Invoke(IEnumerable<Library> libraries, string searchUri)
    {
        return View(new SearchMangaResultViewComponentModel(libraries, searchUri));
    }
}

/// <summary>
/// 
/// </summary>
/// <param name="Libraries"></param>
/// <param name="SearchUri"></param>
public record SearchMangaResultViewComponentModel(IEnumerable<Library> Libraries, string SearchUri);
