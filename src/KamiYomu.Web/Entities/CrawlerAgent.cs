using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.HttpHandlers;

using Microsoft.Extensions.Options;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Entities;
/// <summary>
/// CrawlerAgent represents a dynamically loaded crawler agent assembly that implements the ICrawlerAgent interface. 
/// It provides metadata about the assembly, manages its lifecycle, and allows instantiation of the crawler agent.
/// </summary>
public class CrawlerAgent : IDisposable
{
    public Guid Id { get; private set; }
    public string DisplayName { get; private set; }
    public string AssemblyName { get; private set; }
    public string AssemblyPath { get; private set; }
    public Dictionary<string, object> AgentMetadata { get; private set; }
    public Dictionary<string, string> AssemblyProperties { get; private set; } = [];

    private Assembly _assembly;
    private ICrawlerAgent _crawler;
    private bool disposedValue;

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
        _assembly ??= GetIsolatedAssembly(assemblyPath);
        AssemblyProperties = GetAssemblyMetadata(_assembly);
        _crawler = GetCrawlerInstance();
    }

    /// <summary>
    /// Loads an assembly from the specified path into an isolated <see cref="AssemblyLoadContext"/>.
    /// Validates that the assembly contains at least one non-abstract class implementing <see cref="ICrawlerAgent"/>.
    /// </summary>
    /// <param name="assemblyPath">The file path to the assembly to load.</param>
    /// <returns>The loaded assembly.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the assembly file does not exist at the specified path.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the assembly does not contain any non-abstract class implementing <see cref="ICrawlerAgent"/>.</exception>
    public static Assembly GetIsolatedAssembly(string assemblyPath)
    {

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file not found at path: {assemblyPath}");
        }

        CrawlerAgentLoadContext context = new(assemblyPath);
        Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);

        Type interfaceType = typeof(ICrawlerAgent);
        bool validTypes = assembly.GetTypes().Any(t =>
            t.IsClass &&
            !t.IsAbstract &&
            t.GetInterfaces().Any(i => i.FullName == interfaceType.FullName));

        return !validTypes
            ? throw new InvalidOperationException(
                $"Assembly '{assembly.FullName}' does not contain any non-abstract class implementing '{nameof(ICrawlerAgent)}'.")
            : assembly;
    }

    /// <summary>
    /// Gets or creates a cached instance of the crawler agent from the loaded assembly.
    /// Injects required services such as logger and HTTP message handlers into the crawler instance.
    /// </summary>
    /// <returns>An instance of <see cref="ICrawlerAgent"/> decorated with <see cref="CrawlerAgentDecorator"/>.</returns>
    public ICrawlerAgent GetCrawlerInstance()
    {
        if (_crawler != null)
        {
            return _crawler;
        }

        ILogger logger = ServiceLocator.Instance.GetRequiredService<ILogger<CrawlerAgent>>();
        HttpMessageHandler cloudflareBypassHandler = ServiceLocator.Instance.GetRequiredService<CloudflareBypassHandler>();
        Dictionary<string, object> metadata = new(AgentMetadata)
        {
            [CrawlerAgentMetadata.Fields.KamiYomuILogger] = logger,
            [CrawlerAgentMetadata.Fields.FlareSolverrHttpHandler] = cloudflareBypassHandler
        };

        _crawler = GetCrawlerInstance(AssemblyPath, metadata);
        return _crawler;
    }

    /// <summary>
    /// Creates a new instance of the crawler agent from the assembly at the specified path.
    /// </summary>
    /// <param name="assemblyPath">The file path to the crawler agent assembly.</param>
    /// <param name="options">A dictionary of options and dependencies to inject into the crawler instance constructor.</param>
    /// <returns>An instance of <see cref="ICrawlerAgent"/>.</returns>
    public static ICrawlerAgent GetCrawlerInstance(string assemblyPath, IDictionary<string, object> options)
    {
        Assembly assembly = GetIsolatedAssembly(assemblyPath);
        return GetCrawlerInstance(assembly, options);
    }

    /// <summary>
    /// Creates a new instance of the crawler agent from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing the crawler agent implementation.</param>
    /// <param name="options">A dictionary of options and dependencies to inject into the crawler instance constructor.</param>
    /// <returns>An instance of <see cref="ICrawlerAgent"/> wrapped in a <see cref="CrawlerAgentDecorator"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid crawler type is found in the assembly.</exception>
    /// <exception cref="InvalidCastException">Thrown when the created instance cannot be cast to <see cref="ICrawlerAgent"/>, typically due to assembly load context issues.</exception>
    public static ICrawlerAgent GetCrawlerInstance(Assembly assembly, IDictionary<string, object> options)
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

    /// <summary>
    /// Retrieves assembly metadata for the cached assembly.
    /// </summary>
    /// <returns>A dictionary containing metadata key-value pairs such as Title, Description, Version, etc.</returns>
    public Dictionary<string, string> GetAssemblyMetadata()
    {
        _assembly ??= GetIsolatedAssembly(AssemblyPath);
        return GetAssemblyMetadata(_assembly);
    }

    /// <summary>
    /// Extracts the display name of the crawler agent from the specified assembly.
    /// Uses the <see cref="DisplayNameAttribute"/> if available, otherwise falls back to the assembly full name or "Agent".
    /// </summary>
    /// <param name="assembly">The assembly containing the crawler agent implementation.</param>
    /// <returns>The display name of the crawler agent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid crawler type is found in the assembly.</exception>
    public static string GetCrawlerDisplayName(Assembly assembly)
    {
        Type crawlerType = assembly.GetTypes()
            .FirstOrDefault(t =>
                t.IsClass &&
                !t.IsAbstract &&
                t.GetInterfaces().Any(i => i.FullName == typeof(ICrawlerAgent).FullName))
            ?? throw new InvalidOperationException(I18n.NoValidCrawlerAgentTypeFound);

        return crawlerType.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
            ?? assembly.FullName
            ?? "Agent";
    }

    /// <summary>
    /// Retrieves all input attributes defined on the crawler agent type in the cached assembly.
    /// Includes default system inputs for user agent and timeout configuration.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="AbstractInputAttribute"/> objects.</returns>
    public IEnumerable<AbstractInputAttribute> GetCrawlerInputs()
    {
        _assembly ??= GetIsolatedAssembly(AssemblyPath);
        return GetCrawlerInputs(_assembly);
    }

    /// <summary>
    /// Retrieves all input attributes defined on the crawler agent type in the specified assembly.
    /// Includes default system inputs for user agent and timeout configuration.
    /// </summary>
    /// <param name="assembly">The assembly containing the crawler agent implementation.</param>
    /// <returns>An enumerable collection of <see cref="AbstractInputAttribute"/> objects including both custom and default inputs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid crawler type is found in the assembly.</exception>
    public static IEnumerable<AbstractInputAttribute> GetCrawlerInputs(Assembly assembly)
    {
        Type crawlerType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(ICrawlerAgent).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            ?? throw new InvalidOperationException(I18n.NoValidCrawlerAgentTypeFound);

        List<AbstractInputAttribute> fields = [.. crawlerType.GetCustomAttributes<AbstractInputAttribute>(false)];

        fields.AddRange(
        [
            new CrawlerTextAttribute(CrawlerAgentMetadata.Fields.BrowserUserAgent, I18n.UserAgentExplanation, true, CrawlerAgentMetadata.Values.KamiYomuHttpUserAgent, 900),
            new CrawlerTextAttribute(CrawlerAgentMetadata.Fields.HttpClientTimeout, I18n.TimeoutExplanation, true, CrawlerAgentMetadata.Values.TimeoutMilliseconds.ToString(), 901),
        ]);

        return fields;
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
    /// Get Folder version from the assembly name. It extracts the version number from the assembly name using a regular expression.
    /// </summary>
    /// <returns>The version number as a string in the format "major.minor.patch". If no version is found, returns "0.0.0".</returns>
    public Version GetVersion()
    {
        Dictionary<string, string> metadata = GetAssemblyMetadata();

        return metadata.TryGetValue("Version", out string? version) &&
            Version.TryParse(version, out Version? parsedVersion)
            ? parsedVersion
            : new Version(0, 0, 0);
    }


    /// <summary>
    /// Extracts metadata from the specified assembly, including title, description, company, product, and version information.
    /// </summary>
    /// <param name="assembly">The assembly to extract metadata from.</param>
    /// <returns>A dictionary containing assembly metadata with keys such as FilePath, Title, Description, Company, Product, Version, FileVersion, and InformationalVersion.</returns>
    public static Dictionary<string, string> GetAssemblyMetadata(Assembly assembly)
    {
        Dictionary<string, string> metadata = [];

        //Path
        metadata["FilePath"] = assembly.Location;

        // Title
        AssemblyTitleAttribute? titleAttr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
        if (titleAttr != null)
        {
            metadata["Title"] = titleAttr.Title;
        }

        // Description
        AssemblyDescriptionAttribute? descAttr = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
        if (descAttr != null)
        {
            metadata["Description"] = descAttr.Description;
        }

        // Company
        AssemblyCompanyAttribute? companyAttr = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
        if (companyAttr != null)
        {
            metadata["Company"] = companyAttr.Company;
        }

        // Product
        AssemblyProductAttribute? productAttr = assembly.GetCustomAttribute<AssemblyProductAttribute>();
        if (productAttr != null)
        {
            metadata["Product"] = productAttr.Product;
        }

        // Version
        string? version = assembly.GetName().Version?.ToString();
        if (!string.IsNullOrEmpty(version))
        {
            metadata["Version"] = version;
        }

        // File Version
        AssemblyFileVersionAttribute? fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
        if (fileVersionAttr != null)
        {
            metadata["FileVersion"] = fileVersionAttr.Version;
        }

        // Informational Version
        AssemblyInformationalVersionAttribute? infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (infoVersionAttr != null)
        {
            metadata["InformationalVersion"] = infoVersionAttr.InformationalVersion;
        }

        return metadata;
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
    internal void Update(string? displayName, Dictionary<string, object> agentMetadata, Dictionary<string, string> assemblyProperties)
    {
        DisplayName = displayName;
        AgentMetadata = agentMetadata;
        AssemblyProperties = assemblyProperties;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="CrawlerAgent"/> and optionally releases managed resources.
    /// </summary>
    /// <param name="disposing">A value indicating whether to release both managed and unmanaged resources (true) or only unmanaged resources (false).</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _crawler?.Dispose();
                _crawler = null!;
            }

            disposedValue = true;
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="CrawlerAgent"/> instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
