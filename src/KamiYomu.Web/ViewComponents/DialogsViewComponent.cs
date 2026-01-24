using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class DialogsViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View();
    }
}
