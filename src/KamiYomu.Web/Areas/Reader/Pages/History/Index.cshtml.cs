using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Reader.Pages.History;

public class IndexModel(IChapterProgressRepository chapterProgressRepository) : PageModel
{
    public IEnumerable<IGrouping<DateTime, ChapterViewModel>> GroupedHistory { get; set; } = [];
    public int NextOffset { get; set; }

    public void OnGet(int offset = 0, int length = 20)
    {
        FetchHistory(offset, length);
    }

    public IActionResult OnGetScroll(int offset, int length = 20)
    {
        FetchHistory(offset, length);

        return !GroupedHistory.Any() ? Content("") : Partial("_HistoryListPartial", this);
    }

    private void FetchHistory(int offset, int length)
    {
        GroupedHistory = chapterProgressRepository.FetchHistory(offset, length);

        NextOffset = offset + length;
    }
}
