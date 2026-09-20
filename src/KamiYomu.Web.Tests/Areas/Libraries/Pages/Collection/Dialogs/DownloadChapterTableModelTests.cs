using KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Libraries.Pages.Collection.Dialogs;

public class DownloadChapterTableModelTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.DownloadChapterTableModel", Guid.NewGuid().ToString("N"));
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<IDownloadAppService> _downloadAppService = new();

    public DownloadChapterTableModelTests()
    {
        _ = Directory.CreateDirectory(_rootPath);
        LibraryDbContext.DatabaseFilePathResolver = libraryId => Path.Combine(_rootPath, $"lib{libraryId}.db");
    }

    public void Dispose()
    {
        LibraryDbContext.DatabaseFilePathResolver = libraryId => $"/db/lib{libraryId}.db";
        _dbContext.Dispose();

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

    private DownloadChapterTableModel CreateModel()
    {
        return new DownloadChapterTableModel(_dbContext, _downloadAppService.Object);
    }

    [Fact]
    public void OnGet_WithEmptyLibraryId_DoesNothing()
    {
        DownloadChapterTableModel model = CreateModel();

        model.OnGet(Guid.Empty);

        Assert.Equal(Guid.Empty, model.LibraryId);
        Assert.Empty(model.Records);
        Assert.Equal(0, model.TotalItems);
    }

    [Fact]
    public void OnGet_PaginatesAndOrdersByChapterNumberDescendingByDefault()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext, 1, "Chapter 1");
        using LibraryDbContext libraryDbContext = stored.Library.GetReadWriteDbContext();

        ChapterDownloadRecord second = new(stored.Library.CrawlerAgent, stored.MangaDownload, ServiceTestHelpers.CreateChapter(2, "Chapter 2"));
        ServiceTestHelpers.AssignId(second);
        second.Complete();
        _ = libraryDbContext.ChapterDownloadRecords.Insert(second);

        DownloadChapterTableModel model = CreateModel();
        model.PageSize = 1;
        model.CurrentPage = 1;
        model.SortColumn = nameof(ChapterDownloadRecord.Chapter);
        model.SortAsc = false;

        model.OnGet(stored.Library.Id);

        Assert.Equal(2, model.TotalItems);
        ChapterDownloadRecord record = Assert.Single(model.Records);
        Assert.Equal(2, record.Chapter.Number);
    }

    [Fact]
    public void OnGet_WithUnknownLibraryId_Throws()
    {
        // NOTE: This documents existing production behavior rather than desired behavior. When
        // libraryId doesn't match any stored Library, dbContext.Libraries.FindById returns null and
        // DownloadChapterTableModel.OnGet dereferences it via GetReadOnlyDbContext() without a null
        // check, throwing NullReferenceException instead of e.g. returning empty results. Flagged as
        // a pre-existing bug; not fixed here per refactor-for-testability-only scope.
        DownloadChapterTableModel model = CreateModel();

        _ = Assert.Throws<NullReferenceException>(() => model.OnGet(Guid.NewGuid()));
    }

    [Fact]
    public void OnGetDownloadCbz_WhenServiceReturnsNull_ReturnsNotFound()
    {
        Mock<IZipService> zipService = new();
        _ = zipService.Setup(z => z.GetDownloadCbzResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns((DownloadResponse?)null);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = model.OnGetDownloadCbz(Guid.NewGuid(), Guid.NewGuid(), zipService.Object);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void OnGetDownloadCbz_WhenServiceReturnsResponse_ReturnsFileResult()
    {
        Mock<IZipService> zipService = new();
        DownloadResponse response = new(new MemoryStream([1, 2, 3]), "chapter.cbz", "application/zip");
        _ = zipService.Setup(z => z.GetDownloadCbzResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(response);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = model.OnGetDownloadCbz(Guid.NewGuid(), Guid.NewGuid(), zipService.Object);

        FileStreamResult fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("chapter.cbz", fileResult.FileDownloadName);
        Assert.Equal("application/zip", fileResult.ContentType);
    }

    [Fact]
    public void OnGetDownloadZip_WhenServiceReturnsNull_ReturnsNotFound()
    {
        Mock<IZipService> zipService = new();
        _ = zipService.Setup(z => z.GetDownloadZipResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns((DownloadResponse?)null);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = model.OnGetDownloadZip(Guid.NewGuid(), Guid.NewGuid(), zipService.Object);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void OnGetDownloadPdf_WhenServiceReturnsNull_ReturnsNotFound()
    {
        Mock<IPdfService> pdfService = new();
        _ = pdfService.Setup(z => z.GetDownloadResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns((DownloadResponse?)null);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = model.OnGetDownloadPdf(Guid.NewGuid(), Guid.NewGuid(), pdfService.Object);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void OnGetDownloadEpub_WhenServiceReturnsResponse_ReturnsFileResult()
    {
        Mock<IEpubService> epubService = new();
        DownloadResponse response = new(new MemoryStream([1, 2, 3]), "chapter.epub", "application/epub+zip");
        _ = epubService.Setup(z => z.GetDownloadResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(response);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = model.OnGetDownloadEpub(Guid.NewGuid(), Guid.NewGuid(), epubService.Object);

        FileStreamResult fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("chapter.epub", fileResult.FileDownloadName);
    }

    [Fact]
    public async Task OnPostRescheduleAsync_WhenServiceReturnsNull_ReturnsNotFound()
    {
        _ = _downloadAppService
            .Setup(s => s.RescheduleAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChapterDownloadRecord?)null);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = await model.OnPostRescheduleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostRescheduleAsync_WhenServiceReturnsRecord_ReturnsViewComponentResult()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext);
        _ = _downloadAppService
            .Setup(s => s.RescheduleAsync(stored.Library.Id, stored.ChapterDownload.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored.ChapterDownload);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = await model.OnPostRescheduleAsync(stored.Library.Id, stored.ChapterDownload.Id, CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("DownloadChapterTableRow", viewComponentResult.ViewComponentName);
    }

    [Fact]
    public async Task OnPostCancelAsync_WhenServiceReturnsNull_ReturnsNotFound()
    {
        _ = _downloadAppService
            .Setup(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChapterDownloadRecord?)null);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = await model.OnPostCancelAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostCancelAsync_WhenServiceReturnsRecord_ReturnsViewComponentResult()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext);
        _ = _downloadAppService
            .Setup(s => s.CancelAsync(stored.Library.Id, stored.ChapterDownload.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored.ChapterDownload);

        DownloadChapterTableModel model = CreateModel();

        IActionResult result = await model.OnPostCancelAsync(stored.Library.Id, stored.ChapterDownload.Id, CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("DownloadChapterTableRow", viewComponentResult.ViewComponentName);
    }
}
