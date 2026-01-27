using KamiYomu.Web.Entities;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Reader.ViewComponents;

public class MangaGridItemViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(Library library)
    {
        return View(library);
    }
}
