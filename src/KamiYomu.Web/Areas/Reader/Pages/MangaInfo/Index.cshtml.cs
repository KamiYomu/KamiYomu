using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc.RazorPages;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Reader.Pages.MangaInfo;

public class IndexModel([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
                        [FromKeyedServices(ServiceLocator.ReadOnlyReadingDbContext)] ReadingDbContext readingDbContext) : PageModel
{
    public Library Library { get; set; } = default!;
    public Manga Manga { get; set; } = default!;
    public List<ChapterDownloadRecord> Chapters { get; set; } = [];
    public MangaDownloadRecord MangaDownloadRecord { get; set; }
    public ChapterDownloadRecord? FirstChapterAvailable { get; private set; }
    public ChapterDownloadRecord? CurrentReadingChapter { get; set; }
    public Uri CrawlerAgentFaviconUrl { get; private set; }

    public async Task OnGetAsync(Guid libraryId)
    {
        Library = dbContext.Libraries.Query()
                                   .Where(p => p.Id == libraryId)
                                   .FirstOrDefault();
        Manga = Library.Manga;

        using LibraryDbContext libDb = Library.GetReadOnlyDbContext();

        Chapters = libDb.ChapterDownloadRecords.Query().OrderBy(p => p.Chapter.Number).ToList();

        MangaDownloadRecord = libDb.MangaDownloadRecords.Query().Where(p => p.Library.Id == libraryId).FirstOrDefault();


        ChapterProgress chapterProgress = readingDbContext.ChapterProgress
                                                          .Query()
                                                          .Where(p => p.LibraryId == Library.Id)
                                                          .OrderByDescending(p => p.LastReadAt)
                                                          .FirstOrDefault();

        CurrentReadingChapter = chapterProgress is null ? null : Chapters?.Where(p => p.Id == chapterProgress.ChapterDownloadId).FirstOrDefault();

        FirstChapterAvailable = Chapters?.Where(p => (int)(object)p.DownloadStatus == (int)(object)DownloadStatus.Completed)
                                        .OrderBy(p => p.Chapter.Number)
                                        .FirstOrDefault();

        using ICrawlerAgent crawlerInstance = Library.CrawlerAgent.GetCrawlerInstance();
        CrawlerAgentFaviconUrl = await crawlerInstance.GetFaviconAsync(CancellationToken.None);
    }
}
