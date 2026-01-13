using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Reader.Pages.MangaGallery;

public class IndexModel(DbContext dbContext) : PageModel
{

    public List<Library> Libraries { get; set; } = [];

    public void OnGet()
    {
        Libraries = dbContext.Libraries.Query().ToList();
    }

    public PartialViewResult OnGetSearch(string search)
    {
        List<Manga> filtered = dbContext.Libraries.Query()
                                       .Where(p => p.Manga.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
                                       .Select(p => p.Manga).ToList();

        return Partial("_MangaGrid", filtered);
    }
}
