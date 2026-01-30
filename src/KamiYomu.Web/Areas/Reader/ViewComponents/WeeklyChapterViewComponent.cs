using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Reader.ViewComponents;

public class WeeklyChapterViewComponent(IChapterProgressRepository chapterProgressRepository) : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        IEnumerable<WeeklyChapterViewModel> weeklyChapters = chapterProgressRepository.FetchWeeklyChapters();

        return View(weeklyChapters);
    }
}
