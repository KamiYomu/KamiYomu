using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class CrawlerAgentItemTests
{
    [Fact]
    public void DefaultConstructor_InitializesEmptyDictionaries()
    {
        CrawlerAgentItem item = new();

        Assert.NotNull(item.AgentMetadata);
        Assert.Empty(item.AgentMetadata);
        Assert.NotNull(item.AssemblyProperties);
        Assert.Empty(item.AssemblyProperties);
    }

    [Fact]
    public void Properties_CanBeSetViaInit()
    {
        Guid id = Guid.NewGuid();

        CrawlerAgentItem item = new()
        {
            Id = id,
            DisplayName = "Display Name",
            AssemblyName = "assembly.dll",
            AgentMetadata = new Dictionary<string, object> { ["k"] = "v" },
            AssemblyProperties = new Dictionary<string, string> { ["k2"] = "v2" }
        };

        Assert.Equal(id, item.Id);
        Assert.Equal("Display Name", item.DisplayName);
        Assert.Equal("assembly.dll", item.AssemblyName);
        Assert.Equal("v", item.AgentMetadata["k"]);
        Assert.Equal("v2", item.AssemblyProperties["k2"]);
    }
}
