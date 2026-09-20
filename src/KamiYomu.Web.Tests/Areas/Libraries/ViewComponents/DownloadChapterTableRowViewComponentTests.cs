using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Areas.Libraries.ViewComponents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Routing;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class DownloadChapterTableRowViewComponentTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<IUserClockManager> _userClockManager = new();
    private readonly Mock<IUrlHelper> _urlHelper = new();

    public DownloadChapterTableRowViewComponentTests()
    {
        _ = _userClockManager.Setup(c => c.ConvertToUserTime(It.IsAny<DateTimeOffset>()))
            .Returns((DateTimeOffset dto) => dto);

        RouteData routeData = new();
        routeData.Values["page"] = "/Collection/Dialogs/DownloadChapterTable";
        ActionContext actionContext = new(new DefaultHttpContext(), routeData, new ActionDescriptor());
        _ = _urlHelper.Setup(u => u.ActionContext).Returns(actionContext);
        _ = _urlHelper.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("/mocked-url");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private DownloadChapterTableRowViewComponent CreateComponent()
    {
        return new DownloadChapterTableRowViewComponent(_userClockManager.Object)
        {
            Url = _urlHelper.Object
        };
    }

    [Fact]
    public void Invoke_WithCompletedRecord_PopulatesDownloadAndReaderUrls()
    {
        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(_dbContext);
        DownloadChapterTableRowViewComponent component = CreateComponent();

        IViewComponentResult result = component.Invoke(stored.ChapterDownload);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        DownloadChapterTableRowViewComponentModel model = Assert.IsType<DownloadChapterTableRowViewComponentModel>(viewResult.ViewData.Model);
        Assert.Same(stored.ChapterDownload, model.ChapterDownloadRecord);
        Assert.Equal($"row-{stored.ChapterDownload.Id}", model.RowId);
        Assert.Equal("/mocked-url", model.DownloadCbzUrl);
        Assert.Equal("/mocked-url", model.DownloadZipUrl);
        Assert.Equal("/mocked-url", model.DownloadPdfUrl);
        Assert.Equal("/mocked-url", model.DownloadEpubUrl);
        Assert.Equal("/mocked-url", model.BuiltInReaderUrl);
        Assert.Equal("/mocked-url", model.RescheduleUrl);
        Assert.Equal("#", model.CancelUrl);
    }

    [Fact]
    public void Invoke_WithScheduledRecord_UsesFallbackUrlsForNonApplicableActions()
    {
        Library library = ServiceTestHelpers.CreateLibrary();
        MangaDownloadRecord mangaDownload = new(library, "job-1");
        ServiceTestHelpers.AssignId(mangaDownload);
        Chapter chapter = ServiceTestHelpers.CreateChapter();
        ChapterDownloadRecord chapterDownload = new(library.CrawlerAgent, mangaDownload, chapter);
        ServiceTestHelpers.AssignId(chapterDownload);
        chapterDownload.Scheduled("job-2");

        DownloadChapterTableRowViewComponent component = CreateComponent();

        IViewComponentResult result = component.Invoke(chapterDownload);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        DownloadChapterTableRowViewComponentModel model = Assert.IsType<DownloadChapterTableRowViewComponentModel>(viewResult.ViewData.Model);
        Assert.Equal("#", model.DownloadCbzUrl);
        Assert.Equal("#", model.DownloadZipUrl);
        Assert.Equal("#", model.DownloadPdfUrl);
        Assert.Equal("#", model.DownloadEpubUrl);
        Assert.Equal("#", model.BuiltInReaderUrl);
        Assert.Equal("#", model.RescheduleUrl);
        Assert.Equal("/mocked-url", model.CancelUrl);
    }
}
