using System.IO.Compression;

using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using static KamiYomu.Web.AppOptions.Defaults;

using Library = KamiYomu.Web.Entities.Library;

namespace KamiYomu.Web.Areas.Reader.Pages.MangaReader;

public class IndexModel(
    [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
    ReadingDbContext readingDbContext) : PageModel
{
    public Guid ChapterId { get; set; }
    public ChapterDownloadRecord ChapterDownloaded { get; set; }
    public List<string> PageUrls { get; set; } = [];
    public Guid LibraryId { get; private set; }

    public int LastReadPage { get; private set; }

    public void OnGet(Guid libraryId, Guid chapterId)
    {
        LibraryId = libraryId;
        Library library = dbContext.Libraries.Query()
            .Where(x => x.Id == LibraryId)
            .FirstOrDefault();

        using LibraryDbContext libDb = library.GetReadOnlyDbContext();

        ChapterId = chapterId;

        ChapterDownloaded = libDb.ChapterDownloadRecords.FindOne(p => p.Id == ChapterId);

        string cbzFilePath = library.GetCbzFilePath(ChapterDownloaded.Chapter);

        if (!System.IO.File.Exists(cbzFilePath))
        {
            return;
        }

        using ZipArchive archive = ZipFile.OpenRead(cbzFilePath);

        PageUrls = [.. archive.Entries
            .Where(e => e.FullName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        e.FullName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.FullName.EndsWith("cover" + Path.GetExtension(p.FullName)))
            .OrderBy(e => e.FullName)
            .Select(e => e.FullName)];


        ChapterProgress progress = readingDbContext.ChapterProgress.Query().Where(p => p.LibraryId == libraryId && p.ChapterId == chapterId).FirstOrDefault();

        LastReadPage = progress?.LastPageRead ?? 0;
    }

    public IActionResult OnGetImage(Guid chapterId, Guid libraryId, string fileName)
    {
        Library library = dbContext.Libraries.Query()
            .Where(x => x.Id == libraryId)
            .FirstOrDefault();

        using LibraryDbContext libDb = library.GetReadOnlyDbContext();

        ChapterId = chapterId;

        ChapterDownloadRecord chapterDownloaded = libDb.ChapterDownloadRecords.FindOne(p => p.Id == ChapterId);

        string cbzFilePath = library.GetCbzFilePath(chapterDownloaded.Chapter);

        using ZipArchive archive = ZipFile.OpenRead(cbzFilePath);
        ZipArchiveEntry? entry = archive.GetEntry(fileName);
        if (entry == null)
        {
            return NotFound();
        }

        MemoryStream ms = new();
        using (Stream stream = entry.Open())
        {
            stream.CopyTo(ms);
        }
        ms.Position = 0;

        // Determine content type
        string contentType = UriExtensions.ExtensionToContentType(Path.GetExtension(fileName));
        return File(ms, contentType);
    }

    public IActionResult OnPostPageViewed(Guid libraryId, Guid chapterId, int pageNumber, bool isLastPage)
    {
        ChapterProgress chapterProgres = readingDbContext.ChapterProgress
                                                         .Query()
                                                         .Where(p => p.ChapterId == chapterId && p.LibraryId == libraryId)
                                                         .FirstOrDefault() ?? new(libraryId, chapterId);

        if (isLastPage)
        {
            chapterProgres.SetAsCompleted();
        }
        else
        {
            chapterProgres.SetLastPageRead(pageNumber);
        }

        _ = readingDbContext.ChapterProgress.Upsert(chapterProgres);

        return new EmptyResult();
    }
}
