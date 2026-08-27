using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

using System.Reflection;
using System.Runtime.Loader;

namespace KamiYomu.Web.Entities.CrawlerAgentRuntime;

/// <summary>
/// Provides an isolated, collectible assembly load context for dynamically loaded crawler agents,
/// resolving their dependencies from the agent's installation directory while sharing the main
/// KamiYomu.CrawlerAgents.Core assembly with the host application.
/// </summary>
public sealed class CrawlerAgentLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _baseDir;

    private const string CoreAssemblyName = "KamiYomu.CrawlerAgents.Core";

    public CrawlerAgentLoadContext(string assemblyPath)
        : base(isCollectible: true)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException(
                "Assembly path cannot be empty.",
                nameof(assemblyPath));
        }

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                "Crawler agent assembly was not found.",
                assemblyPath);
        }

        _baseDir = Path.GetDirectoryName(assemblyPath)!;
        _resolver = new AssemblyDependencyResolver(assemblyPath);
    }
    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        /*
         * IMPORTANT:
         *
         * The Core is owned by the main KamiYomu application.
         * Do NOT load another copy of it inside this context.
         *
         * Returning null tells the runtime to resolve it from
         * another AssemblyLoadContext, normally the Default one.
         */
        if (string.Equals(
                assemblyName.Name,
                CoreAssemblyName,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        /*
         * First, let AssemblyDependencyResolver resolve the
         * dependencies declared by the crawler agent.
         */
        string? resolvedPath =
            _resolver.ResolveAssemblyToPath(assemblyName);

        if (!string.IsNullOrEmpty(resolvedPath) &&
            File.Exists(resolvedPath))
        {
            return LoadFromAssemblyPath(resolvedPath);
        }

        /*
         * Fallback: same directory as the crawler agent.
         */
        string localPath = Path.Combine(
            _baseDir,
            $"{assemblyName.Name}.dll");

        if (File.Exists(localPath))
        {
            return LoadFromAssemblyPath(localPath);
        }

        /*
         * Fallback: agent/bin
         */
        string binPath = Path.Combine(
            _baseDir,
            "bin",
            $"{assemblyName.Name}.dll");

        if (File.Exists(binPath))
        {
            return LoadFromAssemblyPath(binPath);
        }

        /*
         * Fallback: agent/obj
         */
        string objPath = Path.Combine(
            _baseDir,
            "obj",
            $"{assemblyName.Name}.dll");

        if (File.Exists(objPath))
        {
            return LoadFromAssemblyPath(objPath);
        }

        /*
         * Final fallback: configured agents directory.
         */
        IOptions<SpecialFolderOptions> specialFolderOptions =
            Defaults.ServiceLocator.Instance
                .GetRequiredService<
                    IOptions<SpecialFolderOptions>>();

        string agentPath = Path.Combine(
            specialFolderOptions.Value.AgentsDir,
            Path.GetFileName(_baseDir),
            $"{assemblyName.Name}.dll");

        return File.Exists(agentPath) ? LoadFromAssemblyPath(agentPath) : null;
    }
}
