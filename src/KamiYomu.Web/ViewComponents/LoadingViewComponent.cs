using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class LoadingViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View();
    }
}
