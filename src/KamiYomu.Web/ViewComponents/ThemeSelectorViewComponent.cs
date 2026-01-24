using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class ThemeSelectorViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View();
    }
}
