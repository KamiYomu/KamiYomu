using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.ViewComponents;

public class LanguageSelectorViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        return View();
    }
}
