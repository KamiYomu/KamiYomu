using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class TimeZoneCheckViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View();
    }
}
