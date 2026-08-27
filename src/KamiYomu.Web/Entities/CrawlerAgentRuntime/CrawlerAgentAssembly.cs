using System.Reflection;

namespace KamiYomu.Web.Entities.CrawlerAgentRuntime;

public sealed record CrawlerAgentAssembly(
    Assembly Assembly,
    CrawlerAgentLoadContext LoadContext);
