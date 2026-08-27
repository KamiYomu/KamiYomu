using KamiYomu.CrawlerAgents.Core;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.HttpHandlers;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Entities.CrawlerAgentRuntime;
/// <summary>
/// CrawlerAgentFactory is responsible for creating instances of crawler agents based on the provided assembly path and metadata.
/// It utilizes the ICrawlerAgentAssemblyLoader to load the assembly and inject necessary dependencies into the crawler agent instance.
/// </summary>
/// <param name="logger"></param>
/// <param name="crawlerAgentAssemblyLoader"></param>
/// <param name="httpClientFactory"></param>
/// <param name="cloudflare"></param>
/// <param name="chromiumHandler"></param>
/// <param name="smartCrawler"></param>
public class CrawlerAgentFactory(
    ILogger<CrawlerAgent> logger,
    ICrawlerAgentAssemblyLoader crawlerAgentAssemblyLoader,
    IHttpClientFactory httpClientFactory,
    CloudflareBypassHandler cloudflare,
    ChromiumHandler chromiumHandler,
    SmartCrawlerHandler smartCrawler) : ICrawlerAgentFactory
{
    /// <inheritdoc/>
    public ICrawlerAgentDecorator Create(CrawlerAgent crawlerAgent)
    {
        ICrawlerAgentDecorator crawlerInstance = Create(crawlerAgent.AssemblyPath, crawlerAgent.AgentMetadata);

        return crawlerInstance;
    }
    /// <inheritdoc/>
    public ICrawlerAgentDecorator Create(string assemblyPath, Dictionary<string, object> metadata)
    {
        Dictionary<string, object> crawlerAgentMetadata = new(metadata)
        {
            [CrawlerAgentMetadata.Fields.KamiYomuILogger] = logger,
            [CrawlerAgentMetadata.Fields.ApplicationHttpClient] = httpClientFactory.CreateClient(CrawlerAgentMetadata.Fields.ApplicationHttpClient),

            [CrawlerAgentMetadata.Fields.FlareSolverrHttpHandler] = cloudflare,
            [CrawlerAgentMetadata.Fields.ChromiumHttpHandler] = chromiumHandler,
            [CrawlerAgentMetadata.Fields.SmartCrawlerHttpHandler] = smartCrawler
        };

        ICrawlerAgentDecorator crawlerInstance = GetCrawlerInstance(assemblyPath, crawlerAgentMetadata);

        return crawlerInstance;
    }

    private ICrawlerAgentDecorator GetCrawlerInstance(string assemblyPath, IDictionary<string, object> options)
    {
        CrawlerAgentAssembly crawlerAssembly = crawlerAgentAssemblyLoader.GetIsolatedAssembly(assemblyPath);
        return crawlerAgentAssemblyLoader.GetCrawlerInstance(crawlerAssembly.Assembly, options);
    }
}
