using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Libraries.ViewComponents;
/// <summary>
/// 
/// </summary>
public class ScanNowButtonViewComponent : ViewComponent
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="viewModel"></param>
    /// <returns></returns>
    public IViewComponentResult Invoke(ScanNowButtonViewModel viewModel)
    {
        return View(viewModel);
    }
}
