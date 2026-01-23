
using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.Pages.History;
using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;
using KamiYomu.Web.Areas.Reader.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using PuppeteerSharp;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Reader.Pages.MangaGallery;

public class IndexModel([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
                        IChapterProgressRepository chapterProgressRepository) : PageModel
{

    public List<Library> Libraries { get; set; } = [];
    public IEnumerable<IGrouping<DateTime, ChapterViewModels>> GroupedHistory { get; private set; }

    public void OnGet()
    {
        Libraries = dbContext.Libraries.Query().ToList();
        GroupedHistory = chapterProgressRepository.FetchHistory(0, 5);
    }

    public PartialViewResult OnGetSearch(string search)
    {
        List<Library> filtered = dbContext.Libraries.Query()
                                       .Where(p => p.Manga.Title.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        return Partial("_MangaGrid", filtered);
    }

    
}
