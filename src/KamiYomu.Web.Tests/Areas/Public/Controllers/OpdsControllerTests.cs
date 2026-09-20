using System.Reflection;
using System.Text;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Public.Controllers;
using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace KamiYomu.Web.Tests.Areas.Public.Controllers;

public class OpdsControllerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly DbContext _dbContext = new(":memory:");
    private readonly OpdsController _controller;

    public OpdsControllerTests()
    {
        Defaults.LiteDbConfig.Configure();

        _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.OpdsController", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_rootPath);
        LibraryDbContext.DatabaseFilePathResolver = libraryId => Path.Combine(_rootPath, $"lib{libraryId}.db");

        _controller = new OpdsController(_dbContext);
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
    public async Task GetMangaList_WithMultiplePages_IncludesFirstLastAndNextLinksButNoPrevious()
    {
        for (int i = 0; i < 5; i++)
        {
            _ = InsertLibrary($"Manga {i}");
        }

        IActionResult result = await _controller.GetMangaList(page: 1, pageSize: 2);

        string xml = await ExecuteAndGetXml(result);

        Assert.Contains("page=2&amp;pageSize=2", GetRelValue(xml, "next"));
        Assert.DoesNotContain("rel=\"previous\"", xml);
        Assert.Contains("page=1&amp;pageSize=2", GetRelValue(xml, "first"));
        Assert.Contains("page=3&amp;pageSize=2", GetRelValue(xml, "last"));
    }

    [Fact]
    public async Task GetMangaList_OnMiddlePage_IncludesPreviousAndNextLinks()
    {
        for (int i = 0; i < 5; i++)
        {
            _ = InsertLibrary($"Manga {i}");
        }

        IActionResult result = await _controller.GetMangaList(page: 2, pageSize: 2);

        string xml = await ExecuteAndGetXml(result);

        Assert.Contains("rel=\"previous\"", xml);
        Assert.Contains("rel=\"next\"", xml);
    }

    [Fact]
    public async Task GetMangaList_ClampsPageSizeAtOneHundred()
    {
        _ = InsertLibrary("Solo Manga");

        IActionResult result = await _controller.GetMangaList(page: 1, pageSize: 500);

        string xml = await ExecuteAndGetXml(result);

        Assert.Contains("pageSize=100", xml);
    }

    [Fact]
    public async Task GetManga_WhenLibraryMissing_ReturnsNotFound()
    {
        IActionResult result = await _controller.GetManga(Guid.NewGuid());

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetManga_WhenLibraryExists_ReturnsFeedWithCompletedChapterEntries()
    {
        Library library = InsertLibrary("Chainsaw Man");
        using LibraryDbContext libraryDbContext = library.GetReadWriteDbContext();
        MangaDownloadRecord mangaDownload = new(library, "job-1");
        ServiceTestHelpers.AssignId(mangaDownload);
        _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownload);

        Chapter chapter = ServiceTestHelpers.CreateChapter(1, "Chapter 1");
        ChapterDownloadRecord chapterDownload = new(library.CrawlerAgent, mangaDownload, chapter);
        ServiceTestHelpers.AssignId(chapterDownload);
        chapterDownload.Complete();
        _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownload);

        IActionResult result = await _controller.GetManga(library.Id);

        string xml = await ExecuteAndGetXml(result);

        Assert.Contains($"urn:opds:manga:{library.Id}", xml);
        Assert.Contains("Chainsaw Man", xml);
        Assert.Contains($"urn:opds:manga:{library.Id}:chapters:{chapterDownload.Id}", xml);
    }

    [Fact]
    public void GetChapter_WhenLibraryMissing_ReturnsNotFound()
    {
        IActionResult result = _controller.GetChapter(Guid.NewGuid(), Guid.NewGuid());

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetChapter_WhenChapterMissing_ReturnsNotFound()
    {
        Library library = InsertLibrary("Vagabond");

        IActionResult result = _controller.GetChapter(library.Id, Guid.NewGuid());

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetChapter_WhenChapterExists_ReturnsFeedWithChapterEntry()
    {
        Library library = InsertLibrary("Vagabond");
        using LibraryDbContext libraryDbContext = library.GetReadWriteDbContext();
        MangaDownloadRecord mangaDownload = new(library, "job-1");
        ServiceTestHelpers.AssignId(mangaDownload);
        _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownload);

        Chapter chapter = ServiceTestHelpers.CreateChapter(3, "Chapter 3");
        ChapterDownloadRecord chapterDownload = new(library.CrawlerAgent, mangaDownload, chapter);
        ServiceTestHelpers.AssignId(chapterDownload);
        chapterDownload.Complete();
        _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownload);

        IActionResult result = _controller.GetChapter(library.Id, chapterDownload.Id);

        string xml = await ExecuteAndGetXml(result);

        Assert.Contains($"urn:opds:manga:{library.Id}:chapter:{chapterDownload.Id}", xml);
    }

    [Fact]
    public void DownloadChapterEpub_WhenServiceReturnsNull_ReturnsNotFound()
    {
        Mock<IEpubService> epubService = new();
        _ = epubService.Setup(s => s.GetDownloadResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns((DownloadResponse?)null);

        IActionResult result = _controller.DownloadChapterEpub(Guid.NewGuid(), Guid.NewGuid(), epubService.Object);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DownloadChapterEpub_WhenServiceReturnsResponse_ReturnsFileResult()
    {
        Mock<IEpubService> epubService = new();
        using MemoryStream content = new(Encoding.UTF8.GetBytes("epub-bytes"));
        DownloadResponse response = new(content, "chapter.epub", "application/epub+zip");
        _ = epubService.Setup(s => s.GetDownloadResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(response);

        IActionResult result = _controller.DownloadChapterEpub(Guid.NewGuid(), Guid.NewGuid(), epubService.Object);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/epub+zip", file.ContentType);
        Assert.Equal("chapter.epub", file.FileDownloadName);
    }

    [Fact]
    public void DownloadChapterCbz_WhenServiceReturnsNull_ReturnsNotFound()
    {
        Mock<IZipService> zipService = new();
        _ = zipService.Setup(s => s.GetDownloadCbzResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns((DownloadResponse?)null);

        IActionResult result = _controller.DownloadChapterCbz(Guid.NewGuid(), Guid.NewGuid(), zipService.Object);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DownloadChapterCbz_WhenServiceReturnsResponse_ReturnsFileResult()
    {
        Mock<IZipService> zipService = new();
        using MemoryStream content = new(Encoding.UTF8.GetBytes("cbz-bytes"));
        DownloadResponse response = new(content, "chapter.cbz", "application/vnd.comicbook+zip");
        _ = zipService.Setup(s => s.GetDownloadCbzResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(response);

        IActionResult result = _controller.DownloadChapterCbz(Guid.NewGuid(), Guid.NewGuid(), zipService.Object);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/vnd.comicbook+zip", file.ContentType);
    }

    [Fact]
    public void DownloadChapterZip_WhenServiceReturnsNull_ReturnsNotFound()
    {
        Mock<IZipService> zipService = new();
        _ = zipService.Setup(s => s.GetDownloadZipResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns((DownloadResponse?)null);

        IActionResult result = _controller.DownloadChapterZip(Guid.NewGuid(), Guid.NewGuid(), zipService.Object);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DownloadChapterZip_WhenServiceReturnsResponse_ReturnsFileResult()
    {
        Mock<IZipService> zipService = new();
        using MemoryStream content = new(Encoding.UTF8.GetBytes("zip-bytes"));
        DownloadResponse response = new(content, "chapter.zip", System.Net.Mime.MediaTypeNames.Application.Zip);
        _ = zipService.Setup(s => s.GetDownloadZipResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(response);

        IActionResult result = _controller.DownloadChapterZip(Guid.NewGuid(), Guid.NewGuid(), zipService.Object);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal(System.Net.Mime.MediaTypeNames.Application.Zip, file.ContentType);
    }

    [Fact]
    public void DownloadChapterPdf_WhenServiceReturnsNull_ReturnsNotFound()
    {
        Mock<IPdfService> pdfService = new();
        _ = pdfService.Setup(s => s.GetDownloadResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns((DownloadResponse?)null);

        IActionResult result = _controller.DownloadChapterZip(Guid.NewGuid(), Guid.NewGuid(), pdfService.Object);

        _ = Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DownloadChapterPdf_WhenServiceReturnsResponse_ReturnsFileResult()
    {
        Mock<IPdfService> pdfService = new();
        using MemoryStream content = new(Encoding.UTF8.GetBytes("pdf-bytes"));
        DownloadResponse response = new(content, "chapter.pdf", System.Net.Mime.MediaTypeNames.Application.Pdf);
        _ = pdfService.Setup(s => s.GetDownloadResponse(It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(response);

        IActionResult result = _controller.DownloadChapterZip(Guid.NewGuid(), Guid.NewGuid(), pdfService.Object);

        FileStreamResult file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal(System.Net.Mime.MediaTypeNames.Application.Pdf, file.ContentType);
    }

    private static async Task<string> ExecuteAndGetXml(IActionResult result)
    {
        using MemoryStream body = new();
        DefaultHttpContext httpContext = new() { Response = { Body = body } };
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());

        await result.ExecuteResultAsync(actionContext);

        body.Position = 0;
        return Encoding.UTF8.GetString(body.ToArray());
    }

    private static string GetRelValue(string xml, string rel)
    {
        int index = xml.IndexOf($"rel=\"{rel}\"", StringComparison.Ordinal);
        Assert.True(index >= 0, $"Expected link with rel=\"{rel}\" in: {xml}");
        int lineStart = xml.LastIndexOf('<', index);
        int lineEnd = xml.IndexOf('>', index);
        return xml[lineStart..lineEnd];
    }

    private Library InsertLibrary(string title)
    {
        Manga manga = MangaBuilder.Create()
            .WithTitle(title)
            .WithOriginalLanguage("ja")
            .WithAuthors(["Author"])
            .WithTags(["tag"])
            .WithCoverUrl(new Uri("https://example.com/cover.png"))
            .WithIsFamilySafe(true)
            .Build();

        CrawlerAgent crawlerAgent = new("Test.Agent.dll", "Test Agent", new Dictionary<string, object>());
        SetId(crawlerAgent, Guid.NewGuid());

        Library library = new(crawlerAgent, manga, "{manga_title}/chapter-{chapter_padded_4}", "{manga_title} ch.{chapter_padded_4}", "{manga_title}");
        SetId(library, Guid.NewGuid());

        _ = _dbContext.Libraries.Insert(library);
        return library;
    }

    private static void SetId(object target, Guid id)
    {
        PropertyInfo property = target.GetType().GetProperty("Id")!;
        _ = property.GetSetMethod(true)!.Invoke(target, [id]);
    }
}
