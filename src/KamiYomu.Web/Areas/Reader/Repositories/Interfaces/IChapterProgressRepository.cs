using KamiYomu.Web.Areas.Reader.ViewModels;

namespace KamiYomu.Web.Areas.Reader.Repositories.Interfaces;

public interface IChapterProgressRepository
{
    IEnumerable<IGrouping<DateTime, ChapterViewModel>> FetchHistory(int offset, int limit);
    IEnumerable<WeeklyChapterViewModel> FetchWeeklyChapters(int showPastDays);
}
