using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.Pages.MangaReader;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Reader.Pages.MangaReader;

public class IndexModelTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.MangaReaderIndexModel", Guid.NewGuid().ToString("N"));
    private readonly DbContext _dbContext = new(":memory:");
    private readonly ReadingDbContext _readingDbContext;

    public IndexModelTests()
    {
        _ = Directory.CreateDirectory(_rootPath);
        _readingDbContext = new ReadingDbContext(Path.Combine(_rootPath, "reading.db"), false);
        LibraryDbContext.DatabaseFilePathResolver = libraryId => Path.Combine(_rootPath, $"lib{libraryId}.db");
    }

    public void Dispose()
    {
        LibraryDbContext.DatabaseFilePathResolver = libraryId => $"/db/lib{libraryId}.db";
        _dbContext.Dispose();
        _readingDbContext.Dispose();

        try
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private StoredLibraryRecord InsertLibraryWithChapter(decimal chapterNumber = 1)
    {
        return ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, chapterNumber, $"Chapter {chapterNumber}");
    }

    [Fact]
    public void OnGet_WhenCbzFileMissing_ReturnsEarlyWithEmptyPageUrls()
    {
        StoredLibraryRecord stored = InsertLibraryWithChapter();
        IndexModel model = new(_dbContext, _readingDbContext);

        model.OnGet(stored.Library.Id, stored.ChapterDownload.Id);

        Assert.Empty(model.PageUrls);
        Assert.Equal(0, model.TotalPages);
    }

    [Fact]
    public void OnGet_WhenCbzFileExists_PopulatesPageUrlsExcludingCoverAndOrdersThem()
    {
        StoredLibraryRecord stored = InsertLibraryWithChapter();
        _ = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "002.jpg", "001.jpg", "cover.jpg", "notes.txt");

        IndexModel model = new(_dbContext, _readingDbContext);

        model.OnGet(stored.Library.Id, stored.ChapterDownload.Id);

        Assert.Equal(["001.jpg", "002.jpg"], model.PageUrls);
        Assert.Equal(2, model.TotalPages);
    }

    [Fact]
    public void OnGet_WhenNoReadingProgress_LastReadPageIsZero()
    {
        StoredLibraryRecord stored = InsertLibraryWithChapter();
        _ = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.jpg");

        IndexModel model = new(_dbContext, _readingDbContext);

        model.OnGet(stored.Library.Id, stored.ChapterDownload.Id);

        Assert.Equal(0, model.LastReadPage);
    }

    [Fact]
    public void OnGet_WhenProgressExistsAndNotCompleted_UsesLastPageRead()
    {
        StoredLibraryRecord stored = InsertLibraryWithChapter();
        _ = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.jpg", "002.jpg");

        ChapterProgress progress = new(stored.Library.Id, stored.ChapterDownload.Id, 1);
        progress.SetLastPageRead(1, 2);
        _ = _readingDbContext.ChapterProgress.Insert(progress);

        IndexModel model = new(_dbContext, _readingDbContext);
        model.OnGet(stored.Library.Id, stored.ChapterDownload.Id);

        Assert.Equal(1, model.LastReadPage);
    }

    [Fact]
    public void OnGet_WhenProgressIsCompleted_LastReadPageIsZero()
    {
        StoredLibraryRecord stored = InsertLibraryWithChapter();
        _ = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.jpg", "002.jpg");

        ChapterProgress progress = new(stored.Library.Id, stored.ChapterDownload.Id, 1);
        progress.SetAsCompleted(2);
        _ = _readingDbContext.ChapterProgress.Insert(progress);

        IndexModel model = new(_dbContext, _readingDbContext);
        model.OnGet(stored.Library.Id, stored.ChapterDownload.Id);

        Assert.Equal(0, model.LastReadPage);
    }

    [Fact]
    public void OnGet_SetsPreviousAndNextChapterIdsOnlyWhenCompleted()
    {
        StoredLibraryRecord chapter2 = InsertLibraryWithChapter(2);
        Library library = chapter2.Library;

        using LibraryDbContext libraryDbContext = library.GetReadWriteDbContext();

        Chapter chapter1 = ServiceTestHelpers.CreateChapter(1, "Chapter 1");
        MangaDownloadRecord mangaDownload1 = new(library, "job-1");
        ServiceTestHelpers.AssignId(mangaDownload1);
        _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownload1);
        ChapterDownloadRecord chapterDownload1 = new(library.CrawlerAgent, mangaDownload1, chapter1);
        ServiceTestHelpers.AssignId(chapterDownload1);
        chapterDownload1.Complete();
        _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownload1);

        Chapter chapter3 = ServiceTestHelpers.CreateChapter(3, "Chapter 3");
        MangaDownloadRecord mangaDownload3 = new(library, "job-3");
        ServiceTestHelpers.AssignId(mangaDownload3);
        _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownload3);
        ChapterDownloadRecord chapterDownload3 = new(library.CrawlerAgent, mangaDownload3, chapter3);
        ServiceTestHelpers.AssignId(chapterDownload3);
        // left as ToBeRescheduled (not completed)
        _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownload3);

        _ = ServiceTestHelpers.CreateCbzFile(library, chapter2.Chapter, "001.jpg");

        IndexModel model = new(_dbContext, _readingDbContext);
        model.OnGet(library.Id, chapter2.ChapterDownload.Id);

        Assert.Equal(chapterDownload1.Id, model.PreviousChapterId);
        Assert.Null(model.NextChapterId);
    }

    [Fact]
    public void OnGetImage_WhenEntryMissing_ReturnsNotFound()
    {
        StoredLibraryRecord stored = InsertLibraryWithChapter();
        _ = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.jpg");

        IndexModel model = new(_dbContext, _readingDbContext);

        IActionResult result = model.OnGetImage(stored.ChapterDownload.Id, stored.Library.Id, "missing.jpg");

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void OnGetImage_WhenEntryExists_ReturnsFileResultWithContentType()
    {
        StoredLibraryRecord stored = InsertLibraryWithChapter();
        _ = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.png");

        IndexModel model = new(_dbContext, _readingDbContext);

        IActionResult result = model.OnGetImage(stored.ChapterDownload.Id, stored.Library.Id, "001.png");

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", file.ContentType);
    }

    [Fact]
    public void OnPostPageViewed_WhenNoExistingProgress_CreatesNewProgress()
    {
        Guid libraryId = Guid.NewGuid();
        Guid chapterDownloadId = Guid.NewGuid();

        IndexModel model = new(_dbContext, _readingDbContext);

        IActionResult result = model.OnPostPageViewed(libraryId, chapterDownloadId, 1, pageNumber: 3, isLastPage: false, totalPages: 10);

        _ = Assert.IsType<EmptyResult>(result);
        ChapterProgress stored = _readingDbContext.ChapterProgress.Query()
            .Where(p => p.LibraryId == libraryId && p.ChapterDownloadId == chapterDownloadId)
            .FirstOrDefault();
        Assert.NotNull(stored);
        Assert.Equal(3, stored.LastPageRead);
        Assert.False(stored.IsCompleted);
    }

    [Fact]
    public void OnPostPageViewed_WhenIsLastPage_MarksProgressCompleted()
    {
        Guid libraryId = Guid.NewGuid();
        Guid chapterDownloadId = Guid.NewGuid();

        IndexModel model = new(_dbContext, _readingDbContext);

        _ = model.OnPostPageViewed(libraryId, chapterDownloadId, 1, pageNumber: 10, isLastPage: true, totalPages: 10);

        ChapterProgress stored = _readingDbContext.ChapterProgress.Query()
            .Where(p => p.LibraryId == libraryId && p.ChapterDownloadId == chapterDownloadId)
            .FirstOrDefault();
        Assert.NotNull(stored);
        Assert.True(stored.IsCompleted);
        Assert.Equal(10, stored.LastPageRead);
    }

    [Fact]
    public void OnPostPageViewed_WhenProgressAlreadyExists_UpdatesExistingRecord()
    {
        Guid libraryId = Guid.NewGuid();
        Guid chapterDownloadId = Guid.NewGuid();
        ChapterProgress existing = new(libraryId, chapterDownloadId, 1);
        existing.SetLastPageRead(2, 10);
        _ = _readingDbContext.ChapterProgress.Insert(existing);

        IndexModel model = new(_dbContext, _readingDbContext);
        _ = model.OnPostPageViewed(libraryId, chapterDownloadId, 1, pageNumber: 5, isLastPage: false, totalPages: 10);

        List<ChapterProgress> all = _readingDbContext.ChapterProgress.Query()
            .Where(p => p.LibraryId == libraryId && p.ChapterDownloadId == chapterDownloadId)
            .ToList();
        ChapterProgress single = Assert.Single(all);
        Assert.Equal(5, single.LastPageRead);
    }
}
