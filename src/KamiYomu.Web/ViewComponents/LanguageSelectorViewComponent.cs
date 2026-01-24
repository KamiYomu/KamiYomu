using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class LanguageSelectorViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View();
    }
}
