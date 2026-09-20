using KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Dialogs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Resources;
using KamiYomu.Web.Tests.Infrastructure.Services;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.CrawlerAgents.Dialogs;

public class ConsolidateCrawlerAgentModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");
    private readonly Mock<ICrawlerAgentAppService> _crawlerAgentAppService = new();
    private readonly Mock<INotificationService> _notificationService = new();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private ConsolidateCrawlerAgentModel CreateModel()
    {
        return new ConsolidateCrawlerAgentModel(_dbContext, _crawlerAgentAppService.Object, _notificationService.Object)
        {
            PageContext = ServiceTestHelpers.CreatePageContext()
        };
    }

    [Fact]
    public void OnGet_PopulatesCrawlerAgentAndLibraryCount()
    {
        CrawlerAgent agent = new("Agent.dll", "Agent", []);
        _ = _dbContext.CrawlerAgents.Insert(agent);

        Library library1 = ServiceTestHelpers.CreateLibrary(agent, "Alpha");
        Library library2 = ServiceTestHelpers.CreateLibrary(agent, "Beta");
        Library otherLibrary = ServiceTestHelpers.CreateLibrary("Gamma");
        _ = _dbContext.Libraries.Insert(library1);
        _ = _dbContext.Libraries.Insert(library2);
        _ = _dbContext.Libraries.Insert(otherLibrary);

        ConsolidateCrawlerAgentModel model = CreateModel();

        model.OnGet(agent.Id);

        Assert.Equal(agent.Id, model.CrawlerAgentId);
        Assert.NotNull(model.CrawlerAgent);
        Assert.Equal(agent.Id, model.CrawlerAgent.Id);
        Assert.Equal(2, model.LibrariesUsingThisCrawlerAgent);
    }

    [Fact]
    public async Task OnPostAsync_ConsolidatesLibrariesAndReturnsPartialWithUpdatedList()
    {
        CrawlerAgent agent = new("Agent.dll", "Agent", []);
        _ = _dbContext.CrawlerAgents.Insert(agent);
        CrawlerAgent otherAgent = new("Other.dll", "Other", []);
        _ = _dbContext.CrawlerAgents.Insert(otherAgent);

        _ = _crawlerAgentAppService
            .Setup(s => s.ConsolidateCollectionByAssemblyNameAsync(It.IsAny<CrawlerAgent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        ConsolidateCrawlerAgentModel model = CreateModel();
        model.CrawlerAgentId = agent.Id;

        IActionResult result = await model.OnPostAsync(CancellationToken.None);

        PartialViewResult partialViewResult = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("_CrawlerAgentList", partialViewResult.ViewName);
        List<CrawlerAgent> model_list = Assert.IsType<List<CrawlerAgent>>(partialViewResult.Model);
        Assert.Equal(2, model_list.Count);

        _crawlerAgentAppService.Verify(
            s => s.ConsolidateCollectionByAssemblyNameAsync(It.Is<CrawlerAgent>(a => a.Id == agent.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationService.Verify(
            n => n.PushSuccessAsync(I18n.AllLibrariesSharedSameCrawlerAgentSuccessfully, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
