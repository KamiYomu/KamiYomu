using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;
using KamiYomu.Web.Areas.Reader.Pages.MangaInfo;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Tests.Infrastructure.Services;

namespace KamiYomu.Web.Tests.Areas.Reader.Pages.MangaInfo;

public class IndexModelTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.MangaInfoIndexModel", Guid.NewGuid().ToString("N"));
    private readonly DbContext _dbContext = new(":memory:");
    private readonly ReadingDbContext _readingDbContext;
    private readonly Mock<ICrawlerAgentFactory> _crawlerAgentFactory = new();
    private readonly Mock<ICrawlerAgentDecorator> _crawlerAgentDecorator = new();

    public IndexModelTests()
    {
        _ = Directory.CreateDirectory(_rootPath);
        _readingDbContext = new ReadingDbContext(Path.Combine(_rootPath, "reading.db"), false);
        LibraryDbContext.DatabaseFilePathResolver = libraryId => Path.Combine(_rootPath, $"lib{libraryId}.db");

        _ = _crawlerAgentFactory.Setup(f => f.Create(It.IsAny<CrawlerAgent>())).Returns(_crawlerAgentDecorator.Object);
        _ = _crawlerAgentDecorator.Setup(c => c.GetFaviconAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Uri("https://example.com/favicon.ico"));
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

    private IndexModel CreateModel()
    {
        return new IndexModel(_dbContext, _readingDbContext, _crawlerAgentFactory.Object);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesLibraryMangaAndChapters()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, 1, "Chapter 1");

        IndexModel model = CreateModel();
        await model.OnGetAsync(stored.Library.Id);

        Assert.Equal(stored.Library.Id, model.Library.Id);
        Assert.Equal(stored.Library.Manga.Id, model.Manga.Id);
        Assert.Same(model.Library.Manga, model.Manga);
        ChapterDownloadRecord chapter = Assert.Single(model.Chapters);
        Assert.Equal(stored.ChapterDownload.Id, chapter.Id);
        Assert.NotNull(model.MangaDownloadRecord);
        Assert.Equal(new Uri("https://example.com/favicon.ico"), model.CrawlerAgentFaviconUrl);
    }

    [Fact]
    public async Task OnGetAsync_SetsFirstChapterAvailableToEarliestCompletedChapter()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, 2, "Chapter 2");
        using LibraryDbContext libraryDbContext = stored.Library.GetReadWriteDbContext();

        Chapter chapter1 = ServiceTestHelpers.CreateChapter(1, "Chapter 1");
        MangaDownloadRecord mangaDownload1 = new(stored.Library, "job-1");
        ServiceTestHelpers.AssignId(mangaDownload1);
        _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownload1);
        ChapterDownloadRecord chapterDownload1 = new(stored.Library.CrawlerAgent, mangaDownload1, chapter1);
        ServiceTestHelpers.AssignId(chapterDownload1);
        chapterDownload1.Complete();
        _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownload1);

        IndexModel model = CreateModel();
        await model.OnGetAsync(stored.Library.Id);

        Assert.NotNull(model.FirstChapterAvailable);
        Assert.Equal(chapterDownload1.Id, model.FirstChapterAvailable.Id);
    }

    [Fact]
    public async Task OnGetAsync_WhenNoReadingProgress_CurrentReadingChapterIsNull()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, 1, "Chapter 1");

        IndexModel model = CreateModel();
        await model.OnGetAsync(stored.Library.Id);

        Assert.Null(model.CurrentReadingChapter);
    }

    [Fact]
    public async Task OnGetAsync_WhenReadingProgressExists_SetsCurrentReadingChapter()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, 1, "Chapter 1");
        ChapterProgress progress = new(stored.Library.Id, stored.ChapterDownload.Id, 1);
        progress.SetLastPageRead(2, 10);
        _ = _readingDbContext.ChapterProgress.Insert(progress);

        IndexModel model = CreateModel();
        await model.OnGetAsync(stored.Library.Id);

        Assert.NotNull(model.CurrentReadingChapter);
        Assert.Equal(stored.ChapterDownload.Id, model.CurrentReadingChapter.Id);
    }
}
