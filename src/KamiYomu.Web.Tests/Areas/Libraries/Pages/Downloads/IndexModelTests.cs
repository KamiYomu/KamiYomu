using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Libraries.Pages.Downloads;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Models;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Areas.Libraries.Pages.Downloads;

public class IndexModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<IDownloadAppService> _downloadAppService = new();
    private readonly Mock<ICrawlerAgentRepository> _crawlerAgentRepository = new();
    private readonly IOptions<WorkerOptions> _workerOptions = Options.Create(new WorkerOptions
    {
        ServerAvailableNames = ["server-1"],
        DownloadChapterQueues = ["download"],
        MangaDownloadSchedulerQueues = ["manga"],
        DiscoveryNewChapterQueues = ["discovery"],
        DailyExecutionTime = TimeSpan.FromHours(3)
    });

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private IndexModel CreateModel()
    {
        return new IndexModel(_dbContext, _workerOptions, _downloadAppService.Object, _crawlerAgentRepository.Object)
        {
            MangaId = string.Empty,
            ComicInfoTitleTemplate = string.Empty,
            ComicInfoSeriesTemplate = string.Empty,
            MakeThisConfigurationDefault = false,
            PageContext = ServiceTestHelpers.CreatePageContext()
        };
    }

    private static void SetUrlHelper(IndexModel model, string pageName)
    {
        Mock<IUrlHelper> urlHelper = new();
        RouteData routeData = new();
        routeData.Values["page"] = pageName;
        ActionContext actionContext = new(model.PageContext.HttpContext, routeData, new ActionDescriptor());
        _ = urlHelper.Setup(u => u.ActionContext).Returns(actionContext);
        _ = urlHelper.Setup(u => u.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("/mocked-url");
        model.Url = urlHelper.Object;
    }

    [Fact]
    public void OnGet_PopulatesCrawlerAgentsFromDbContext()
    {
        CrawlerAgent agent = new("Test.Agent.dll", "Test Agent", []);
        typeof(CrawlerAgent).GetProperty(nameof(CrawlerAgent.Id))!.SetValue(agent, Guid.NewGuid());
        _ = _dbContext.CrawlerAgents.Insert(agent);

        IndexModel model = CreateModel();

        model.OnGet();

        CrawlerAgent result = Assert.Single(model.CrawlerAgents);
        Assert.Equal(agent.Id, result.Id);
    }

    [Fact]
    public async Task OnGetSearchAsync_WithBlankQuery_ReturnsEmptyResult()
    {
        IndexModel model = CreateModel();
        model.Query = "";
        model.SelectedAgent = Guid.NewGuid();

        IActionResult result = await model.OnGetSearchAsync(CancellationToken.None);

        _ = Assert.IsType<EmptyResult>(result);
        _crawlerAgentRepository.Verify(
            r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnGetSearchAsync_WithoutSelectedAgent_ReturnsEmptyResult()
    {
        IndexModel model = CreateModel();
        model.Query = "one piece";
        model.SelectedAgent = null;

        IActionResult result = await model.OnGetSearchAsync(CancellationToken.None);

        _ = Assert.IsType<EmptyResult>(result);
    }

    [Fact]
    public async Task OnGetSearchAsync_WhenCrawlerAgentDoesNotExist_ReturnsEmptyResult()
    {
        IndexModel model = CreateModel();
        model.Query = "one piece";
        model.SelectedAgent = Guid.NewGuid();

        IActionResult result = await model.OnGetSearchAsync(CancellationToken.None);

        _ = Assert.IsType<EmptyResult>(result);
    }

    [Fact]
    public async Task OnGetSearchAsync_WithoutHxRequestHeader_ReturnsPageAndPopulatesResults()
    {
        _ = _dbContext.UserPreferences.Insert(new UserPreference(System.Globalization.CultureInfo.GetCultureInfo("en-US")));
        CrawlerAgent agent = new("Test.Agent.dll", "Test Agent", []);
        typeof(CrawlerAgent).GetProperty(nameof(CrawlerAgent.Id))!.SetValue(agent, Guid.NewGuid());
        _ = _dbContext.CrawlerAgents.Insert(agent);

        Manga manga = MangaBuilder.Create().WithTitle("One Piece").WithIsFamilySafe(true).Build();
        PagedResult<Manga> pagedResult = PagedResultBuilder<Manga>.Create()
            .WithData([manga])
            .WithPaginationOptions(new PaginationOptions(0, 30, 1))
            .Build();
        _ = _crawlerAgentRepository
            .Setup(r => r.SearchAsync(agent.Id, "one piece", It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        IndexModel model = CreateModel();
        model.Query = "one piece";
        model.SelectedAgent = agent.Id;

        IActionResult result = await model.OnGetSearchAsync(CancellationToken.None);

        _ = Assert.IsType<PageResult>(result);
        _ = Assert.Single(model.Results);
        _ = Assert.Single(model.CrawlerAgents);
    }

    [Fact]
    public async Task OnGetSearchAsync_WithHxRequestHeader_ReturnsSearchMangaResultViewComponent()
    {
        _ = _dbContext.UserPreferences.Insert(new UserPreference(System.Globalization.CultureInfo.GetCultureInfo("en-US")));
        CrawlerAgent agent = new("Test.Agent.dll", "Test Agent", []);
        typeof(CrawlerAgent).GetProperty(nameof(CrawlerAgent.Id))!.SetValue(agent, Guid.NewGuid());
        _ = _dbContext.CrawlerAgents.Insert(agent);

        Manga manga = MangaBuilder.Create().WithTitle("Naruto").WithIsFamilySafe(true).Build();
        PagedResult<Manga> pagedResult = PagedResultBuilder<Manga>.Create()
            .WithData([manga])
            .WithPaginationOptions(new PaginationOptions(0, 30, 1))
            .Build();
        _ = _crawlerAgentRepository
            .Setup(r => r.SearchAsync(agent.Id, "naruto", It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        IndexModel model = CreateModel();
        model.PageContext = ServiceTestHelpers.CreatePageContext(httpContext => httpContext.Request.Headers["HX-Request"] = "true");
        SetUrlHelper(model, "/Downloads/Index");
        model.Query = "naruto";
        model.SelectedAgent = agent.Id;

        IActionResult result = await model.OnGetSearchAsync(CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("SearchMangaResult", viewComponentResult.ViewComponentName);
    }

    [Fact]
    public async Task OnPostAddToCollectionAsync_WhenModelStateInvalid_ReturnsBadRequest()
    {
        IndexModel model = CreateModel();
        model.ModelState.AddModelError("MangaId", "Required");

        IActionResult result = await model.OnPostAddToCollectionAsync(CancellationToken.None);

        _ = Assert.IsType<BadRequestObjectResult>(result);
        _downloadAppService.Verify(
            s => s.AddToCollectionAsync(It.IsAny<AddItemCollection>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostAddToCollectionAsync_WhenModelStateValid_ReturnsLibraryCardViewComponent()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Alpha");
        _ = _downloadAppService
            .Setup(s => s.AddToCollectionAsync(It.IsAny<AddItemCollection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(library);

        IndexModel model = CreateModel();

        IActionResult result = await model.OnPostAddToCollectionAsync(CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("LibraryCard", viewComponentResult.ViewComponentName);
    }

    [Fact]
    public async Task OnPostRemoveFromCollectionAsync_WhenTemplateFieldsInvalid_IgnoresThemAndProceeds()
    {
        Library library = ServiceTestHelpers.CreateLibrary("Beta");
        _ = _downloadAppService
            .Setup(s => s.RemoveFromCollectionAsync(It.IsAny<RemoveItemCollection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(library);

        IndexModel model = CreateModel();
        model.ModelState.AddModelError(nameof(IndexModel.FilePathTemplate), "Required");
        model.ModelState.AddModelError(nameof(IndexModel.ComicInfoTitleTemplate), "Required");
        model.ModelState.AddModelError(nameof(IndexModel.ComicInfoSeriesTemplate), "Required");

        IActionResult result = await model.OnPostRemoveFromCollectionAsync(CancellationToken.None);

        ViewComponentResult viewComponentResult = Assert.IsType<ViewComponentResult>(result);
        Assert.Equal("LibraryCard", viewComponentResult.ViewComponentName);
    }

    [Fact]
    public async Task OnPostRemoveFromCollectionAsync_WhenOtherFieldInvalid_ReturnsBadRequest()
    {
        IndexModel model = CreateModel();
        model.ModelState.AddModelError("MangaId", "Required");

        IActionResult result = await model.OnPostRemoveFromCollectionAsync(CancellationToken.None);

        _ = Assert.IsType<BadRequestObjectResult>(result);
        _downloadAppService.Verify(
            s => s.RemoveFromCollectionAsync(It.IsAny<RemoveItemCollection>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
