using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities.CrawlerAgentRuntime;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.HttpHandlers;

using Microsoft.Extensions.Options;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Entities;
/// <summary>
/// CrawlerAgent represents a dynamically loaded crawler agent assembly that implements the ICrawlerAgent interface. 
/// It provides metadata about the assembly, manages its lifecycle, and allows instantiation of the crawler agent.
/// </summary>
public class CrawlerAgent
{
    public Guid Id { get; private set; }
    public string DisplayName { get; private set; }
    public string AssemblyName { get; private set; }
    public string AssemblyPath { get; private set; }
    public Dictionary<string, object> AgentMetadata { get; private set; }
    public Dictionary<string, string> AssemblyProperties { get; private set; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CrawlerAgent"/> class with default values.
    /// </summary>
    public CrawlerAgent()
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrawlerAgent"/> class with the specified assembly path, display name, and metadata.
    /// </summary>
    /// <param name="assemblyPath">The file path to the crawler agent assembly.</param>
    /// <param name="displayName">The display name for the crawler agent. If null or whitespace, the assembly file name without extension is used.</param>
    /// <param name="agentMetadata">A dictionary containing metadata to be passed to the crawler agent during instantiation.</param>
    public CrawlerAgent(string assemblyPath, string? displayName, Dictionary<string, object> agentMetadata)
    {
        AssemblyPath = assemblyPath;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(assemblyPath) : displayName;
        AssemblyName = Path.GetFileName(assemblyPath);
        AgentMetadata = agentMetadata;
    }

    public void UpdateAssemblyProperties(Dictionary<string, string> assemblyProperties)
    {
        AssemblyProperties = assemblyProperties;
    }

    /// <summary>
    /// Loads an assembly from the specified path into an isolated <see cref="AssemblyLoadContext"/>.
    /// Validates that the assembly contains at least one non-abstract class implementing <see cref="ICrawlerAgent"/>.
    /// </summary>
    /// <param name="assemblyPath">The file path to the assembly to load.</param>
    /// <returns>The loaded assembly.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the assembly file does not exist at the specified path.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the assembly does not contain any non-abstract class implementing <see cref="ICrawlerAgent"/>.</exception>
    public static CrawlerAgentAssembly GetIsolatedAssembly(
    string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"Assembly file not found at path: {assemblyPath}");
        }

        CrawlerAgentLoadContext context = new CrawlerAgentLoadContext(assemblyPath);

        try
        {
            Assembly assembly =
                context.LoadFromAssemblyPath(
                    Path.GetFullPath(assemblyPath));

            Type interfaceType = typeof(ICrawlerAgent);

            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                IEnumerable<string> loaderExceptions = ex.LoaderExceptions
                    .Where(e => e != null)
                    .Select(e => e!.ToString());

                throw new InvalidOperationException(
                    $"Failed to load types from crawler agent assembly " +
                    $"'{assembly.FullName}'. " +
                    $"Loader errors: {string.Join(
                        Environment.NewLine,
                        loaderExceptions)}",
                    ex);
            }

            bool validTypes = types.Any(type =>
                type is
                {
                    IsClass: true,
                    IsAbstract: false
                }
                &&
                type.GetInterfaces().Any(
                    implementedInterface =>
                        implementedInterface == interfaceType));

            if (!validTypes)
            {
                throw new InvalidOperationException(
                    $"Assembly '{assembly.FullName}' does not contain " +
                    $"any non-abstract class implementing " +
                    $"'{nameof(ICrawlerAgent)}'.");
            }

            return new CrawlerAgentAssembly(
                assembly,
                context);
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    /// <summary>
    /// Deletes the directory containing the crawler agent assembly and all associated files.
    /// </summary>
    public void DeleteAssembly()
    {
        string dir = GetCrawlerAgentDir(AssemblyName);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Checks if the crawler agent directory exists.
    /// </summary>
    /// <returns>True if the directory exists; otherwise, false.</returns>
    public bool IsCrawlerAgentExists()
    {
        string dir = GetCrawlerAgentDir(AssemblyName);
        return Directory.Exists(dir);
    }
    
    /// <summary>
    /// Gets or creates the directory path for the specified agent assembly file.
    /// </summary>
    /// <param name="fileName">The name of the agent assembly file.</param>
    /// <returns>The full directory path where the agent files are stored.</returns>
    public static string GetCrawlerAgentDir(string fileName)
    {
        IOptions<SpecialFolderOptions> specialFolderOptions = ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();

        string name = GetAgentDirName(fileName);

        string directory = Path.Combine(specialFolderOptions.Value.AgentsDir, name);

        _ = Directory.CreateDirectory(directory);

        return directory;
    }



    /// <summary>
    /// Extracts the directory name from the given assembly file name by removing the file extension.
    /// </summary>
    /// <param name="fileName">The assembly file name.</param>
    /// <returns>The directory name without file extension.</returns>
    public static string GetAgentDirName(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName);
    }

    /// <summary>
    /// Extracts the DLL file name from an assembly file name by removing version suffixes.
    /// Attempts to parse semantic versioning components and removes them from the end of the file name.
    /// </summary>
    /// <param name="fileName">The assembly file name, potentially containing version information.</param>
    /// <returns>The DLL file name with version suffixes removed.</returns>
    public static string GetAgentDllFileName(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        string[] parts = name.Split('.');

        for (int i = parts.Length - 1; i >= 2; i--)
        {
            string patchPart = parts[i].Split('-')[0]; // remove prerelease suffix
            string minorPart = parts[i - 1];
            string majorPart = parts[i - 2];

            if (int.TryParse(majorPart, out _) &&
                int.TryParse(minorPart, out _) &&
                int.TryParse(patchPart, out _))
            {
                return string.Join('.', parts.Take(i - 2));
            }
        }

        return name;
    }

    /// <summary>
    /// Updates the crawler agent's display name, metadata, and assembly properties.
    /// </summary>
    /// <param name="displayName">The new display name for the crawler agent.</param>
    /// <param name="agentMetadata">The updated metadata dictionary for the agent.</param>
    /// <param name="assemblyProperties">The updated assembly properties dictionary.</param>
    public void Update(string? displayName, Dictionary<string, object> agentMetadata, Dictionary<string, string> assemblyProperties)
    {
        DisplayName = displayName;
        AgentMetadata = agentMetadata;
        AssemblyProperties = assemblyProperties;
    }
}
