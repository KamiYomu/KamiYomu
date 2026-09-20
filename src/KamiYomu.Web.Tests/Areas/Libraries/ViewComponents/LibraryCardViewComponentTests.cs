using KamiYomu.Web.Areas.Libraries.ViewComponents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.Areas.Libraries.ViewComponents;

public class LibraryCardViewComponentTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ICrawlerAgentFactory> _crawlerAgentFactory = new();
    private readonly Mock<ICrawlerAgentDecorator> _crawlerAgentDecorator = new();

    public LibraryCardViewComponentTests()
    {
        _ = _crawlerAgentFactory.Setup(f => f.Create(It.IsAny<CrawlerAgent>())).Returns(_crawlerAgentDecorator.Object);
        _ = _crawlerAgentDecorator.Setup(c => c.GetFaviconAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Uri("https://example.com/favicon.ico"));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private LibraryCardViewComponent CreateComponent()
    {
        return new LibraryCardViewComponent(_dbContext, _crawlerAgentFactory.Object);
    }

    [Fact]
    public async Task InvokeAsync_WhenCrawlerAgentExists_ResolvesFaviconAndUrls()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _dbContext.CrawlerAgents.Insert(library.CrawlerAgent);

        LibraryCardViewComponent component = CreateComponent();

        IViewComponentResult result = await component.InvokeAsync(library);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        LibraryCardViewComponentModel model = Assert.IsType<LibraryCardViewComponentModel>(viewResult.ViewData.Model);
        Assert.Same(library, model.Library);
        Assert.Equal(new Uri("https://example.com/favicon.ico"), model.FaviconUrl);
        Assert.False(model.CrawlerAgentDisabled);
        Assert.False(model.IsNew);
        Assert.Contains(library.CrawlerAgent.Id.ToString(), model.AddToCollectionUrl);
        Assert.Contains(library.Id.ToString(), model.RemoveFromCollectionUrl);
        Assert.Contains(library.Id.ToString(), model.DownloadStatusUrl);
        Assert.Contains(library.CrawlerAgent.Id.ToString(), model.MangaDetailsUrl);
        Assert.Contains(library.Id.ToString(), model.UpgradeCrawlerAgentUrl);
    }

    [Fact]
    public async Task InvokeAsync_WhenCrawlerAgentMissing_UsesDefaultFaviconAndDisablesCrawler()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Beta");

        LibraryCardViewComponent component = CreateComponent();

        IViewComponentResult result = await component.InvokeAsync(library);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        LibraryCardViewComponentModel model = Assert.IsType<LibraryCardViewComponentModel>(viewResult.ViewData.Model);
        Assert.Equal(new Uri("/images/favicon.ico", UriKind.Relative), model.FaviconUrl);
        Assert.True(model.CrawlerAgentDisabled);
        _crawlerAgentFactory.Verify(f => f.Create(It.IsAny<CrawlerAgent>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenLibraryIdIsEmpty_MarksLibraryAsNew()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Gamma");
        typeof(Library).GetProperty(nameof(Library.Id))!.SetValue(library, Guid.Empty);

        LibraryCardViewComponent component = CreateComponent();

        IViewComponentResult result = await component.InvokeAsync(library);

        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        LibraryCardViewComponentModel model = Assert.IsType<LibraryCardViewComponentModel>(viewResult.ViewData.Model);
        Assert.True(model.IsNew);
    }
}
