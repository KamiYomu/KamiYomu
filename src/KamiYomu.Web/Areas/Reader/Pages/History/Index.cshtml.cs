using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Reader.Pages.History;

public class IndexModel([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext, [FromKeyedServices(ServiceLocator.ReadOnlyReadingDbContext)] ReadingDbContext readingDbContext) : PageModel
{
    // Ensure these are public properties
    public IEnumerable<IGrouping<DateTime, ChapterModel>> GroupedHistory { get; set; } = [];
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
        List<ChapterProgress> chapterProgresses = readingDbContext.ChapterProgress.Query()
            .OrderByDescending(p => p.LastReadAt)
            .Skip(offset)
            .Limit(length)
            .ToList();

        List<Guid> libraryIds = [.. chapterProgresses.Select(cp => cp.LibraryId).Distinct()];
        Dictionary<Guid, Library> libraries = dbContext.Libraries.Query()
            .Where(l => libraryIds.Contains(l.Id))
            .ToList()
            .ToDictionary(l => l.Id);

        GroupedHistory = chapterProgresses
            .Select(cp => new ChapterModel
            {
                ChapterProgress = cp,
                Library = libraries.GetValueOrDefault(cp.LibraryId)
            })
            .GroupBy(cm => cm.ChapterProgress.LastReadAt.Date)
            .OrderByDescending(g => g.Key);

        NextOffset = offset + length;
    }
}


public class ChapterModel
{
    public ChapterProgress ChapterProgress { get; set; }
    public Library Library { get; set; }
}
