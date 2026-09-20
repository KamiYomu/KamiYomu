using KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

namespace KamiYomu.Web.Tests.Areas.Settings.Pages.CrawlerAgents;

public class IndexModelTests : IDisposable
{
    private readonly DbContext _dbContext = new(":memory:");

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void OnGet_WhenNoCrawlerAgentsExist_PopulatesEmptyCollection()
    {
        IndexModel model = new(_dbContext);

        model.OnGet();

        Assert.NotNull(model.CrawlerAgents);
        Assert.Empty(model.CrawlerAgents);
    }

    [Fact]
    public void OnGet_WhenCrawlerAgentsExist_PopulatesAllOfThem()
    {
        CrawlerAgent agentOne = new("AgentOne.dll", "Agent One", []);
        CrawlerAgent agentTwo = new("AgentTwo.dll", "Agent Two", []);
        _ = _dbContext.CrawlerAgents.Insert(agentOne);
        _ = _dbContext.CrawlerAgents.Insert(agentTwo);

        IndexModel model = new(_dbContext);

        model.OnGet();

        Assert.NotNull(model.CrawlerAgents);
        Assert.Equal(2, model.CrawlerAgents.Count());
        Assert.Contains(model.CrawlerAgents, a => a.DisplayName == "Agent One");
        Assert.Contains(model.CrawlerAgents, a => a.DisplayName == "Agent Two");
    }
}
