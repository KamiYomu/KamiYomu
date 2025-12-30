using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

public class RemoveFromCollectionModel(DbContext dbContext) : PageModel
{

    public Guid LibraryId { get; set; }

    public required string RefreshElementId { get; set; }

    public required Entities.Library Library { get; set; }

    public void OnGet(Guid libraryId, string refreshElementId)
    {
        LibraryId = libraryId;
        RefreshElementId = refreshElementId;
        Library = dbContext.Libraries.Include(p => p.Manga).Include(p => p.CrawlerAgent).FindOne(p => p.Id == LibraryId);
    }
}
