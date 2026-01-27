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
    public List<Library> RecentlyAddedLibraries { get; set; } = [];
    public List<Library> Libraries { get; set; } = [];
    public IEnumerable<IGrouping<DateTime, ChapterViewModel>> GroupedHistory { get; private set; }

    public void OnGet()
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();
        RecentlyAddedLibraries = dbContext.Libraries.Query()
                                       .Where(p => p.Manga.IsFamilySafe || !userPreference.FamilySafeMode)
                                       .OrderByDescending(p => p.CreatedDate)
                                       .Limit(5)
                                       .ToList();

        IEnumerable<Guid> existingIds = RecentlyAddedLibraries.Select(q => q.Id);

        Libraries = dbContext.Libraries.Query().Where(p => (p.Manga.IsFamilySafe || !userPreference.FamilySafeMode)
                                                         && !existingIds.Contains(p.Id)).ToList();
        GroupedHistory = chapterProgressRepository.FetchHistory(0, 5);
    }

    public PartialViewResult OnGetSearch(string search)
    {
        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();

        List<Library> filtered = dbContext.Libraries.Query()
                                       .Where(p =>
                                       p.Manga.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                                       && (p.Manga.IsFamilySafe || !userPreference.FamilySafeMode)).ToList();

        return Partial("_MangaGrid", filtered);

    }
}
