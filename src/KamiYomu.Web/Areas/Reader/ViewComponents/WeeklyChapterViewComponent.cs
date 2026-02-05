using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Reader.ViewComponents;

public class WeeklyChapterViewComponent(IChapterProgressRepository chapterProgressRepository) : ViewComponent
{
    public IViewComponentResult Invoke(int showPastDays)
    {
        IEnumerable<WeeklyChapterViewModel> weeklyChapters = chapterProgressRepository.FetchWeeklyChapters(showPastDays);

        return View(new WeeklyChapterViewComponentModel
        {
            Limit = showPastDays,
            WeeklyChapters = weeklyChapters
        });
    }
}


public record WeeklyChapterViewComponentModel
{
    public int Limit { get; init; }
    public IEnumerable<WeeklyChapterViewModel> WeeklyChapters { get; init; }
}
