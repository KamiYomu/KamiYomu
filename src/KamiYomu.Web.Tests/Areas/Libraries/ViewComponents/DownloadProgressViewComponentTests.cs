using KamiYomu.Web.Areas.Libraries.ViewComponents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class DownloadProgressViewComponentTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.DownloadProgressViewComponent", Guid.NewGuid().ToString("N"));

    public DownloadProgressViewComponentTests()
    {
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
    }

    [Fact]
    public void Invoke_WhenNoMangaDownloadRecordExists_ReturnsEmptyContent()
    {
        Library library = ServiceTestHelpers.CreateLibrary();
        DownloadProgressViewComponent component = new();

        IViewComponentResult result = component.Invoke(library);

        ContentViewComponentResult contentResult = Assert.IsType<ContentViewComponentResult>(result);
        Assert.Equal(string.Empty, contentResult.Content);
    }

    [Fact]
    public void Invoke_WithMixOfCompletedAndPendingChapters_ComputesProgressPercentage()
    {
        Library library = ServiceTestHelpers.CreateLibrary();
        using LibraryDbContext libraryDbContext = library.GetReadWriteDbContext();
        MangaDownloadRecord mangaDownload = new(library, "job-1");
        ServiceTestHelpers.AssignId(mangaDownload);
        _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownload);

        ChapterDownloadRecord completed = new(library.CrawlerAgent, mangaDownload, ServiceTestHelpers.CreateChapter(1, "Chapter 1"));
        ServiceTestHelpers.AssignId(completed);
        completed.Complete();
        _ = libraryDbContext.ChapterDownloadRecords.Insert(completed);

        ChapterDownloadRecord pending = new(library.CrawlerAgent, mangaDownload, ServiceTestHelpers.CreateChapter(2, "Chapter 2"));
        ServiceTestHelpers.AssignId(pending);
        _ = libraryDbContext.ChapterDownloadRecords.Insert(pending);

        DownloadProgressViewComponent component = new();

        IViewComponentResult result = component.Invoke(library);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        DownloadProgressViewComponentModel model = Assert.IsType<DownloadProgressViewComponentModel>(viewResult.ViewData.Model);
        Assert.Equal(2, model.Total);
        Assert.Equal(1, model.Completed);
        Assert.Equal(50, model.Progress);
        Assert.Equal(mangaDownload.Id, model.DownloadManga.Id);
    }
}
