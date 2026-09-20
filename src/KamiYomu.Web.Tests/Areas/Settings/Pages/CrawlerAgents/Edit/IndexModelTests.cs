using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Edit;
using KamiYomu.Web.Areas.Settings.Pages.Shared;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

using MonkeyCache;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.CrawlerAgents.Edit;

public class IndexModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly CacheContext _cacheContext = new();
    private readonly Mock<ICrawlerAgentAssemblyLoader> _crawlerAgentAssemblyLoader = new();
    private readonly Mock<ICrawlerAgentAppService> _crawlerAgentAppService = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly IOptions<CloudflareSolverOptions> _cloudflareSolverOptions = Options.Create(new CloudflareSolverOptions());
    private readonly Func<IBarrel> _originalCacheResolver = CacheContext.CurrentResolver;

    public IndexModelTests()
    {
        Mock<IBarrel> barrel = new();
        _ = barrel.Setup(b => b.GetKeys(It.IsAny<CacheState>())).Returns([]);
        CacheContext.CurrentResolver = () => barrel.Object;

        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetCrawlerInputs(It.IsAny<CrawlerAgent>()))
            .Returns([new CrawlerTextAttribute("ApiKey", "Api Key", true, null, 1)]);
        _ = _crawlerAgentAssemblyLoader
            .Setup(l => l.GetAssemblyMetadata(It.IsAny<CrawlerAgent>()))
            .Returns(new Dictionary<string, string> { ["Version"] = "1.0.0" });
    }

    public void Dispose()
    {
        CacheContext.CurrentResolver = _originalCacheResolver;
        _dbContext.Dispose();
    }

    private IndexModel CreateModel()
    {
        return new IndexModel(
            _dbContext,
            _cacheContext,
            _cloudflareSolverOptions,
            _crawlerAgentAssemblyLoader.Object,
            _crawlerAgentAppService.Object,
            _notificationService.Object);
    }

    private CrawlerAgent InsertAgent(string displayName = "Agent")
    {
        CrawlerAgent agent = new("Agent.dll", displayName, []);
        _ = _dbContext.CrawlerAgents.Insert(agent);
        return agent;
    }

    [Fact]
    public void OnGet_WithExistingAgentAndNoLibraries_PopulatesInputAndReturnsPage()
    {
        CrawlerAgent agent = InsertAgent();

        IndexModel model = CreateModel();
        model.Id = agent.Id;

        IActionResult result = model.OnGet();

        _ = Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Input);
        Assert.Equal(agent.Id, model.Input.Id);
        Assert.Equal(agent.DisplayName, model.Input.DisplayName);
        Assert.Equal(0, model.LibrariesUsingThisCrawlerAgent);
        Assert.False(model.RequiresConfirmation);
        Assert.Equal("1.0.0", model.Input.ReadOnlyMetadata["Version"]);
        _ = Assert.Single(model.Input.CrawlerInputsViewModel.CrawlerInputs);
    }

    [Fact]
    public void OnGet_WithLibrariesUsingAgent_SetsRequiresConfirmation()
    {
        CrawlerAgent agent = InsertAgent();
        Library library = ServiceTestHelpers.CreateLibrary(agent, "Alpha");
        _ = _dbContext.Libraries.Insert(library);

        IndexModel model = CreateModel();
        model.Id = agent.Id;

        _ = model.OnGet();

        Assert.Equal(1, model.LibrariesUsingThisCrawlerAgent);
        Assert.True(model.RequiresConfirmation);
    }

    [Fact]
    public void OnGet_WithUnknownAgentId_Throws()
    {
        // FetchData() throws when the CrawlerAgent cannot be found and OnGet does not catch it;
        // documenting this actual (arguably risky) production behavior rather than papering over it.
        IndexModel model = CreateModel();
        model.Id = Guid.NewGuid();

        _ = Assert.Throws<InvalidOperationException>(() => model.OnGet());
    }

    [Fact]
    public async Task OnPostSaveAsync_WithInvalidModelState_ReturnsPageWithoutSaving()
    {
        CrawlerAgent agent = InsertAgent("Original Name");

        IndexModel model = CreateModel();
        model.Id = agent.Id;
        model.Input = new InputModel { Id = agent.Id, DisplayName = "Attempted Name" };
        model.ModelState.AddModelError("DisplayName", "Required");

        IActionResult result = await model.OnPostSaveAsync(CancellationToken.None);

        _ = Assert.IsType<PageResult>(result);
        Assert.Equal("Original Name", _dbContext.CrawlerAgents.FindById(agent.Id).DisplayName);
        _notificationService.Verify(n => n.EnqueueErrorForNextPage(I18n.PleaseCorrectHighlightedField), Times.Once);
    }

    [Fact]
    public async Task OnPostSaveAsync_WhenConfirmationRequired_ShowsDialogWithoutSaving()
    {
        CrawlerAgent agent = InsertAgent("Original Name");

        IndexModel model = CreateModel();
        model.Id = agent.Id;
        model.Input = new InputModel { Id = agent.Id, DisplayName = "Attempted Name" };
        model.RequiresConfirmation = true;

        IActionResult result = await model.OnPostSaveAsync(CancellationToken.None);

        _ = Assert.IsType<PageResult>(result);
        Assert.True(model.ShowConfirmationDialog);
        Assert.Equal("Original Name", _dbContext.CrawlerAgents.FindById(agent.Id).DisplayName);
    }

    [Fact]
    public async Task OnPostSaveAsync_WhenNoConfirmationNeeded_UpdatesAgentAndRedirects()
    {
        CrawlerAgent agent = InsertAgent("Original Name");

        IndexModel model = CreateModel();
        model.Id = agent.Id;
        model.Input = new InputModel
        {
            Id = agent.Id,
            DisplayName = "Updated Name",
            CrawlerInputsViewModel = new CrawlerInputsViewModel
            {
                AgentMetadata = new Dictionary<string, string?> { ["ApiKey"] = "secret" }
            },
            ReadOnlyMetadata = []
        };
        model.RequiresConfirmation = false;

        IActionResult result = await model.OnPostSaveAsync(CancellationToken.None);

        RedirectToPageResult redirectResult = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/CrawlerAgents/Edit/Index", redirectResult.PageName);

        CrawlerAgent updated = _dbContext.CrawlerAgents.FindById(agent.Id);
        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal("secret", updated.AgentMetadata["ApiKey"]);
        _notificationService.Verify(n => n.EnqueueSuccessForNextPage(I18n.CrawlerAgentSavedSuccessfully), Times.Once);
        _crawlerAgentAppService.Verify(
            s => s.ConsolidateCollectionByCrawlerAgentAsync(It.IsAny<CrawlerAgent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostSaveAsync_WithMissingRequiredMetadata_ReturnsPageWithoutSaving()
    {
        CrawlerAgent agent = InsertAgent("Original Name");

        IndexModel model = CreateModel();
        model.Id = agent.Id;
        model.Input = new InputModel
        {
            Id = agent.Id,
            DisplayName = "Updated Name",
            CrawlerInputsViewModel = new CrawlerInputsViewModel
            {
                AgentMetadata = new Dictionary<string, string?> { ["ApiKey"] = string.Empty }
            },
            ReadOnlyMetadata = []
        };
        model.RequiresConfirmation = false;

        IActionResult result = await model.OnPostSaveAsync(CancellationToken.None);

        _ = Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.Equal("Original Name", _dbContext.CrawlerAgents.FindById(agent.Id).DisplayName);
    }
}
