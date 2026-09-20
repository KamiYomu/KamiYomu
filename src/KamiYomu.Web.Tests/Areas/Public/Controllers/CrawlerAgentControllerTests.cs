using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Areas.Public.Controllers;
using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Models;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Public.Controllers;

public class CrawlerAgentControllerTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ICrawlerAgentRepository> _repository = new();
    private readonly CrawlerAgentController _controller;

    public CrawlerAgentControllerTests()
    {
        KamiYomu.Web.AppOptions.Defaults.LiteDbConfig.Configure();
        _controller = new CrawlerAgentController(_repository.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void List_WithoutSearch_ReturnsAllAgents()
    {
        CrawlerAgent agent1 = InsertCrawlerAgent("agent-one.dll", "Agent One");
        CrawlerAgent agent2 = InsertCrawlerAgent("agent-two.dll", "Agent Two");

        IActionResult result = _controller.List(search: null, offSet: 0, limit: 20, dbContext: _dbContext);

        List<CrawlerAgentItem> items = GetOkValues(result);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Id == agent1.Id);
        Assert.Contains(items, i => i.Id == agent2.Id);
    }

    [Fact]
    public void List_WithSearch_FiltersByDisplayNameOrAssemblyName()
    {
        CrawlerAgent matching = InsertCrawlerAgent("mangadex.dll", "MangaDex");
        _ = InsertCrawlerAgent("other.dll", "Other Agent");

        IActionResult result = _controller.List(search: "MangaDex", offSet: 0, limit: 20, dbContext: _dbContext);

        List<CrawlerAgentItem> items = GetOkValues(result);
        CrawlerAgentItem item = Assert.Single(items);
        Assert.Equal(matching.Id, item.Id);
    }

    [Fact]
    public async Task ListDownloadableContentAsync_WithEmptyCrawlerAgentId_ReturnsNotFound()
    {
        IActionResult result = await _controller.ListDownloadableContentAsync(Guid.Empty, "search");

        _ = Assert.IsType<NotFoundResult>(result);
        _repository.Verify(
            r => r.SearchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ListDownloadableContentAsync_WithContinuationToken_UsesContinuationTokenPagination()
    {
        Guid agentId = Guid.NewGuid();
        Manga manga = MangaBuilder.Create().WithTitle("Test Manga").Build();
        PagedResult<Manga> expected = PagedResultBuilder<Manga>.Create()
            .WithData([manga])
            .WithPaginationOptions(new PaginationOptions("token-123", 15))
            .Build();

        PaginationOptions? captured = null;
        _ = _repository
            .Setup(r => r.SearchAsync(agentId, "one piece", It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, PaginationOptions, CancellationToken>((_, _, options, _) => captured = options)
            .ReturnsAsync(expected);

        IActionResult result = await _controller.ListDownloadableContentAsync(agentId, "one piece", offSet: 0, limit: 15, continuationToken: "token-123");

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.NotNull(captured);
        Assert.Equal("token-123", captured!.ContinuationToken);
        Assert.Equal(15, captured.Total);
    }

    [Fact]
    public async Task ListDownloadableContentAsync_WithoutContinuationToken_UsesOffsetLimitPagination()
    {
        Guid agentId = Guid.NewGuid();
        PagedResult<Manga> expected = PagedResultBuilder<Manga>.Create()
            .WithData([])
            .WithPaginationOptions(new PaginationOptions(5, 10, 10))
            .Build();

        PaginationOptions? captured = null;
        _ = _repository
            .Setup(r => r.SearchAsync(agentId, "search", It.IsAny<PaginationOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, PaginationOptions, CancellationToken>((_, _, options, _) => captured = options)
            .ReturnsAsync(expected);

        IActionResult result = await _controller.ListDownloadableContentAsync(agentId, "search", offSet: 5, limit: 10);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        Assert.NotNull(captured);
        Assert.Equal(5, captured!.OffSet);
        Assert.Equal(10, captured.Limit);
        Assert.Null(captured.ContinuationToken);
    }

    [Fact]
    public async Task AddDownloadContentAsync_WithEmptyCrawlerAgentId_ReturnsBadRequest()
    {
        Mock<IDownloadAppService> downloadAppService = new();
        AddItemCollection request = new() { CrawlerAgentId = Guid.Empty, MangaId = "manga-1" };

        IActionResult result = await _controller.AddDownloadContentAsync(request, downloadAppService.Object, CancellationToken.None);

        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result);
        PublicApiErrorResponse error = Assert.IsType<PublicApiErrorResponse>(badRequest.Value);
        Assert.Equal("InvalidCrawlerAgentId", error.Error);
        downloadAppService.Verify(s => s.AddToCollectionAsync(It.IsAny<AddItemCollection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDownloadContentAsync_WithValidRequest_ReturnsCreatedWithCollectionItem()
    {
        CrawlerAgent crawlerAgent = new("agent.dll", "Agent", []);
        SetId(crawlerAgent, Guid.NewGuid());
        Manga manga = MangaBuilder.Create().WithTitle("Vinland Saga").Build();
        Library library = new(crawlerAgent, manga, null, null, null);
        SetId(library, Guid.NewGuid());

        AddItemCollection request = new() { CrawlerAgentId = crawlerAgent.Id, MangaId = manga.Id };
        Mock<IDownloadAppService> downloadAppService = new();
        _ = downloadAppService
            .Setup(s => s.AddToCollectionAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(library);

        IActionResult result = await _controller.AddDownloadContentAsync(request, downloadAppService.Object, CancellationToken.None);

        CreatedResult created = Assert.IsType<CreatedResult>(result);
        Assert.Equal($"/public/api/v1/collection/{library.Id}", created.Location);
        CollectionItem item = Assert.IsType<CollectionItem>(created.Value);
        Assert.Equal(library.Id, item.LibraryId);
        Assert.Equal(crawlerAgent.Id, item.CrawlerAgentId);
    }

    private static void SetId(object target, Guid id)
    {
        PropertyInfo property = target.GetType().GetProperty("Id")!;
        _ = property.GetSetMethod(true)!.Invoke(target, [id]);
    }

    private static List<CrawlerAgentItem> GetOkValues(IActionResult result)
    {
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        return ((IEnumerable<CrawlerAgentItem>)ok.Value!).ToList();
    }

    private CrawlerAgent InsertCrawlerAgent(string assemblyName, string displayName)
    {
        CrawlerAgent agent = new(assemblyName, displayName, []);
        SetId(agent, Guid.NewGuid());
        _ = _dbContext.CrawlerAgents.Insert(agent);
        return agent;
    }
}
