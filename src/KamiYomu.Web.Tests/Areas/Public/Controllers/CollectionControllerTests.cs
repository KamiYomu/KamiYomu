using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Areas.Public.Controllers;
using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Models;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Public.Controllers;

public class CollectionControllerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly DbContext _dbContext = new(":memory:");
    private readonly CollectionController _controller = new();

    public CollectionControllerTests()
    {
        KamiYomu.Web.AppOptions.Defaults.LiteDbConfig.Configure();

        _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.CollectionController", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_rootPath);
        LibraryDbContext.DatabaseFilePathResolver = libraryId => Path.Combine(_rootPath, $"lib{libraryId}.db");
    }

    public void Dispose()
    {
        LibraryDbContext.DatabaseFilePathResolver = libraryId => $"/db/lib{libraryId}.db";
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

        _dbContext.Dispose();
    }

    [Fact]
    public void List_WithoutSearch_ReturnsAllCollectionItems()
    {
        Library library1 = InsertLibrary("Alpha Manga");
        Library library2 = InsertLibrary("Beta Manga");

        IActionResult result = _controller.List(search: null, offSet: 0, limit: 20, dbContext: _dbContext);

        List<CollectionItem> items = GetOkValues(result);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.LibraryId == library1.Id);
        Assert.Contains(items, i => i.LibraryId == library2.Id);
    }

    [Fact]
    public void List_WithSearch_FiltersByMangaTitle()
    {
        Library matching = InsertLibrary("Naruto Shippuden");
        _ = InsertLibrary("One Piece");

        IActionResult result = _controller.List(search: "Naruto", offSet: 0, limit: 20, dbContext: _dbContext);

        List<CollectionItem> items = GetOkValues(result);
        CollectionItem item = Assert.Single(items);
        Assert.Equal(matching.Id, item.LibraryId);
    }

    [Fact]
    public void List_RespectsOffsetAndLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            _ = InsertLibrary($"Manga {i}");
        }

        IActionResult result = _controller.List(search: null, offSet: 2, limit: 2, dbContext: _dbContext);

        List<CollectionItem> items = GetOkValues(result);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Get_WhenLibraryExists_ReturnsCollectionItem()
    {
        Library library = InsertLibrary("Bleach");

        IActionResult result = _controller.Get(library.Id, _dbContext);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        CollectionItem item = Assert.IsType<CollectionItem>(ok.Value);
        Assert.Equal(library.Id, item.LibraryId);
        Assert.Equal(library.CrawlerAgent.Id, item.CrawlerAgentId);
    }

    [Fact]
    public void Get_WhenLibraryMissing_ReturnsNotFound()
    {
        IActionResult result = _controller.Get(Guid.NewGuid(), _dbContext);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveAsync_WhenLibraryMissing_ReturnsNotFoundAndDoesNotCallService()
    {
        Mock<IDownloadAppService> downloadAppService = new();

        IActionResult result = await _controller.RemoveAsync(Guid.NewGuid(), downloadAppService.Object, _dbContext, CancellationToken.None);

        _ = Assert.IsType<NotFoundResult>(result);
        downloadAppService.Verify(s => s.RemoveFromCollectionAsync(It.IsAny<RemoveItemCollection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_WhenLibraryExists_RemovesAndReturnsOk()
    {
        Library library = InsertLibrary("Death Note");
        Mock<IDownloadAppService> downloadAppService = new();
        _ = downloadAppService
            .Setup(s => s.RemoveFromCollectionAsync(It.Is<RemoveItemCollection>(r => r.CrawlerAgentId == library.CrawlerAgent.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(library);

        IActionResult result = await _controller.RemoveAsync(library.Id, downloadAppService.Object, _dbContext, CancellationToken.None);

        _ = Assert.IsType<OkResult>(result);
        downloadAppService.Verify(s => s.RemoveFromCollectionAsync(It.IsAny<RemoveItemCollection>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetChapters_WhenLibraryMissing_ReturnsNotFound()
    {
        IActionResult result = _controller.GetChapters(Guid.NewGuid(), 0, 20, _dbContext);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetChapters_WhenLibraryExists_ReturnsMappedChapterItems()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext);

        IActionResult result = _controller.GetChapters(stored.Library.Id, 0, 20, _dbContext);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        List<ChapterItem> items = ((IEnumerable<ChapterItem?>)ok.Value!).Where(i => i != null).Cast<ChapterItem>().ToList();
        ChapterItem item = Assert.Single(items);
        Assert.Equal(stored.ChapterDownload.Id, item.ChapterDownloadId);
        Assert.Equal(stored.Library.Id, item.LibraryId);
    }

    [Fact]
    public void GetChapter_WhenLibraryMissing_ReturnsNotFound()
    {
        IActionResult result = _controller.GetChapter(Guid.NewGuid(), Guid.NewGuid(), _dbContext);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetChapter_WhenChapterMissing_ReturnsNotFound()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext);

        IActionResult result = _controller.GetChapter(stored.Library.Id, Guid.NewGuid(), _dbContext);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetChapter_WhenChapterExists_ReturnsMappedChapterItem()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext);

        IActionResult result = _controller.GetChapter(stored.Library.Id, stored.ChapterDownload.Id, _dbContext);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        ChapterItem item = Assert.IsType<ChapterItem>(ok.Value);
        Assert.Equal(stored.ChapterDownload.Id, item.ChapterDownloadId);
    }

    [Fact]
    public void GetChaptersAvailable_WhenLibraryMissing_ReturnsNotFound()
    {
        IActionResult result = _controller.GetChaptersAvailable(Guid.NewGuid(), 0, 20, _dbContext);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetChaptersAvailable_OnlyReturnsCompletedChapters()
    {
        StoredLibraryRecord completed = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, 1, "Chapter 1");

        using LibraryDbContext libraryDbContext = completed.Library.GetReadWriteDbContext();
        MangaDownloadRecord pendingMangaDownload = new(completed.Library, "manga-job-pending");
        ServiceTestHelpers.AssignId(pendingMangaDownload);
        _ = libraryDbContext.MangaDownloadRecords.Insert(pendingMangaDownload);

        Chapter pendingChapter = ServiceTestHelpers.CreateChapter(2, "Chapter 2");
        ChapterDownloadRecord pendingChapterDownload = new(completed.Library.CrawlerAgent, pendingMangaDownload, pendingChapter);
        ServiceTestHelpers.AssignId(pendingChapterDownload);
        _ = libraryDbContext.ChapterDownloadRecords.Insert(pendingChapterDownload);

        IActionResult result = _controller.GetChaptersAvailable(completed.Library.Id, 0, 20, _dbContext);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        List<ChapterItem> items = ((IEnumerable<ChapterItem?>)ok.Value!).Where(i => i != null).Cast<ChapterItem>().ToList();
        ChapterItem item = Assert.Single(items);
        Assert.Equal(completed.ChapterDownload.Id, item.ChapterDownloadId);
    }

    [Fact]
    public async Task RescheduleChapterAsync_WhenRecordNotFound_ReturnsNotFound()
    {
        Mock<IDownloadAppService> downloadAppService = new();
        _ = downloadAppService
            .Setup(s => s.RescheduleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChapterDownloadRecord?)null);

        IActionResult result = await _controller.RescheduleChapterAsync(Guid.NewGuid(), Guid.NewGuid(), downloadAppService.Object, CancellationToken.None);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RescheduleChapterAsync_WhenRecordFound_ReturnsOkWithChapterItem()
    {
        Library library = ServiceTestHelpers.CreateLibrary();
        Chapter chapter = ServiceTestHelpers.CreateChapter();
        MangaDownloadRecord mangaDownload = new(library, "job-1");
        ChapterDownloadRecord record = new(library.CrawlerAgent, mangaDownload, chapter);
        ServiceTestHelpers.AssignId(record);

        Mock<IDownloadAppService> downloadAppService = new();
        _ = downloadAppService
            .Setup(s => s.RescheduleAsync(library.Id, record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        IActionResult result = await _controller.RescheduleChapterAsync(library.Id, record.Id, downloadAppService.Object, CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        ChapterItem item = Assert.IsType<ChapterItem>(ok.Value);
        Assert.Equal(record.Id, item.ChapterDownloadId);
    }

    [Fact]
    public async Task CancelChapterAsync_WhenRecordNotFound_ReturnsNotFound()
    {
        Mock<IDownloadAppService> downloadAppService = new();
        _ = downloadAppService
            .Setup(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChapterDownloadRecord?)null);

        IActionResult result = await _controller.CancelChapterAsync(Guid.NewGuid(), Guid.NewGuid(), downloadAppService.Object, CancellationToken.None);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CancelChapterAsync_WhenRecordFound_ReturnsOkWithChapterItem()
    {
        Library library = ServiceTestHelpers.CreateLibrary();
        Chapter chapter = ServiceTestHelpers.CreateChapter();
        MangaDownloadRecord mangaDownload = new(library, "job-1");
        ChapterDownloadRecord record = new(library.CrawlerAgent, mangaDownload, chapter);
        ServiceTestHelpers.AssignId(record);
        record.Cancelled("user requested");

        Mock<IDownloadAppService> downloadAppService = new();
        _ = downloadAppService
            .Setup(s => s.CancelAsync(library.Id, record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        IActionResult result = await _controller.CancelChapterAsync(library.Id, record.Id, downloadAppService.Object, CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        ChapterItem item = Assert.IsType<ChapterItem>(ok.Value);
        Assert.Equal(DownloadStatus.Cancelled, item.DownloadStatus);
    }

    private static List<CollectionItem> GetOkValues(IActionResult result)
    {
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        return ((IEnumerable<CollectionItem>)ok.Value!).ToList();
    }

    private Library InsertLibrary(string title)
    {
        Library library = ServiceTestHelpers.CreateLibrary(title);
        _ = _dbContext.Libraries.Insert(library);
        return library;
    }
}
