using KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Dialogs;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.CrawlerAgents.Dialogs;

public class CrawlerAgentDetailsModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void OnGet_WithExistingCrawlerAgent_PopulatesAgent()
    {
        CrawlerAgent agent = new("Agent.dll", "My Agent", []);
        _ = _dbContext.CrawlerAgents.Insert(agent);

        AgentDetailsModel model = new(_dbContext) { Agent = null! };

        model.OnGet(agent.Id);

        Assert.NotNull(model.Agent);
        Assert.Equal(agent.Id, model.Agent.Id);
        Assert.Equal("My Agent", model.Agent.DisplayName);
    }

    [Fact]
    public void OnGet_WithUnknownId_LeavesAgentNull()
    {
        AgentDetailsModel model = new(_dbContext) { Agent = null! };

        // NOTE: LiteDB's FindById returns null (not a not-found exception) for a missing document.
        // AgentDetailsModel.Agent is declared as `required` (non-nullable) but the runtime value is
        // actually null here, documenting the existing behavior rather than the compile-time contract.
        model.OnGet(Guid.NewGuid());

        Assert.Null(model.Agent);
    }
}
