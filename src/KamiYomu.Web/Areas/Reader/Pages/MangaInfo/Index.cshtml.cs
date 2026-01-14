using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Reader.Pages.MangaInfo;

public class IndexModel(DbContext dbContext) : PageModel
{
    public Library Library { get; set; } = default!;
    public Manga Manga { get; set; } = default!;
    public List<ChapterDownloadRecord> Chapters { get; set; } = [];
    public MangaDownloadRecord MangaDownloadRecord { get; set; }

    public void OnGet(string id)
    {
        Library = dbContext.Libraries.Query()
                                   .Where(p => p.Manga.Id == id)
                                   .FirstOrDefault();
        Manga = Library.Manga;

        using LibraryDbContext libDb = Library.GetReadOnlyDbContext();

        Chapters = libDb.ChapterDownloadRecords.Query().OrderBy(p => p.Chapter.Number).ToList();

        MangaDownloadRecord = libDb.MangaDownloadRecords.Query().Where(p => p.Library.Manga.Id == id).FirstOrDefault();
    }
}
