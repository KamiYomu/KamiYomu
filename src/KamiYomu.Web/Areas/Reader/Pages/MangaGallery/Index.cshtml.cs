using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Reader.Pages.MangaGallery;

public class IndexModel([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
                        IChapterProgressRepository chapterProgressRepository) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<Library> RecentlyAddedLibraries { get; set; } = [];
    public List<Library> Libraries { get; set; } = [];
    public IEnumerable<IGrouping<DateTime, ChapterViewModel>> GroupedHistory { get; private set; }

    public void OnGet()
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();
        RecentlyAddedLibraries = dbContext.Libraries.Query()
                                       .Where(p => p.Manga.IsFamilySafe || p.Manga.IsFamilySafe == userPreference.FamilySafeMode)
                                       .OrderByDescending(p => p.CreatedDate)
                                       .Limit(5)
                                       .ToList();


        Libraries = dbContext.Libraries.Query().Where(p => p.Manga.IsFamilySafe || p.Manga.IsFamilySafe == userPreference.FamilySafeMode).ToList();
        GroupedHistory = chapterProgressRepository.FetchHistory(0, 5);
    }

    public IActionResult OnGetSearch()
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();

        RecentlyAddedLibraries = dbContext.Libraries.Query()
                               .Where(p => p.Manga.IsFamilySafe || !userPreference.FamilySafeMode)
                               .OrderByDescending(p => p.CreatedDate)
                               .Limit(5)
                               .ToList();

        Libraries = dbContext.Libraries.Query()
                                       .Where(p =>
                                       p.Manga.Title.Contains(Search, StringComparison.OrdinalIgnoreCase)
                                       && (p.Manga.IsFamilySafe || !userPreference.FamilySafeMode)).ToList();

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Partial("_MangaGrid", Libraries);
        }

        GroupedHistory = chapterProgressRepository.FetchHistory(0, 5);

        return Page();

    }
}
