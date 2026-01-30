using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using LiteDB;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Reader.Pages.MangaGallery;

public class IndexModel([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
                        IChapterProgressRepository chapterProgressRepository) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SortColumn { get; set; } = nameof(ChapterDownloadRecord.Chapter);

    [BindProperty(SupportsGet = true)]
    public bool SortAsc { get; set; } = true;

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;
    public List<Library> RecentlyAddedLibraries { get; set; } = [];
    public List<Library> Libraries { get; set; } = [];
    public int TotalItems { get; set; } = 0;
    public IEnumerable<IGrouping<DateTime, ChapterViewModel>> GroupedHistory { get; private set; }
    public void OnGet()
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();
        RecentlyAddedLibraries = dbContext.Libraries.Query()
                                       .Where(p => p.Manga.IsFamilySafe || p.Manga.IsFamilySafe == userPreference.FamilySafeMode)
                                       .OrderByDescending(p => p.CreatedDate)
                                       .Limit(5)
                                       .ToList();


        Libraries = dbContext.Libraries
                             .Query()
                             .Where(p => p.Manga.IsFamilySafe || p.Manga.IsFamilySafe == userPreference.FamilySafeMode)
                             .Skip((CurrentPage - 1) * PageSize)
                             .Limit(PageSize)
                             .ToList();

        TotalItems = dbContext.Libraries
                         .Query()
                         .Where(p => p.Manga.IsFamilySafe || p.Manga.IsFamilySafe == userPreference.FamilySafeMode)
                         .Count();


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
        TotalItems = dbContext.Libraries
         .Query()
         .Where(p => p.Manga.IsFamilySafe || p.Manga.IsFamilySafe == userPreference.FamilySafeMode)
         .Count();

        ILiteQueryable<Library> query = dbContext.Libraries.Query()
                                                           .Where(p => p.Manga.Title.Contains(Search, StringComparison.OrdinalIgnoreCase)
                                                                   && (p.Manga.IsFamilySafe || !userPreference.FamilySafeMode));

        Libraries = query.Skip((CurrentPage - 1) * PageSize).Limit(PageSize).ToList();

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Partial("_PagedMangaGrid", this);
        }

        GroupedHistory = chapterProgressRepository.FetchHistory(0, 5);

        return Page();

    }
}
