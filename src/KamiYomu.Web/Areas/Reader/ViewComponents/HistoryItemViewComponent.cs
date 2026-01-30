using KamiYomu.Web.Areas.Reader.ViewModels;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Reader.ViewComponents;

public class HistoryItemViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ChapterViewModel viewModel)
    {
        return View(viewModel);
    }
}
