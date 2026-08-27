using System.Reflection;

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
    public ICrawlerAgentDecorator Create(string assemblyPath, IDictionary<string, object> metadata)
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

    /// <inheritdoc/>
    public ICrawlerAgentDecorator Create(Assembly assembly, IDictionary<string, object> options)
    {
        string interfaceName = typeof(ICrawlerAgent).FullName!;

        Type crawlerType = assembly.GetTypes()
            .FirstOrDefault(t =>
                t.IsClass &&
                !t.IsAbstract &&
                t.GetInterfaces().Any(i => i.FullName == interfaceName))
            ?? throw new InvalidOperationException(I18n.NoValidCrawlerAgentTypeFound);

        object instance = Activator.CreateInstance(crawlerType, options)
            ?? throw new InvalidOperationException("Failed to create crawler instance.");

        // Safe cast only if type identity matches
        if (instance is ICrawlerAgent typedInstance)
        {
            return new CrawlerAgentDecorator(typedInstance);
        }

        // Fallback: wrap dynamically if cast fails
        throw new InvalidCastException(
            $"The type '{crawlerType.FullName}' could not be cast to '{interfaceName}'. " +
            $"This usually means the interface was loaded in a different AssemblyLoadContext. " +
            $"Ensure both the main app and the plugin reference the same shared interface assembly, " +
            $"and that it is loaded only once in the default context.");
    }

    private ICrawlerAgentDecorator GetCrawlerInstance(string assemblyPath, IDictionary<string, object> options)
    {
        CrawlerAgentAssembly crawlerAssembly = crawlerAgentAssemblyLoader.GetIsolatedAssembly(assemblyPath);
        return Create(crawlerAssembly.Assembly, options);
    }
}
