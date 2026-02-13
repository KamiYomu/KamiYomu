namespace KamiYomu.Web.Areas.Public.Models;

public record CrawlerAgentItem
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; }
    public string AssemblyName { get; init; }
    public Dictionary<string, object> AgentMetadata { get; set; } = [];
    public Dictionary<string, string> AssemblyProperties { get; set; } = [];
}
