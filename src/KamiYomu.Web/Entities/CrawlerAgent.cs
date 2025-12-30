using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

using System.ComponentModel;
using System.Reflection;

namespace KamiYomu.Web.Entities;

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

    public CrawlerAgent()
    {

    }

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

    public ICrawlerAgent GetCrawlerInstance()
    {
        if (_crawler != null)
        {
            return _crawler;
        }

        ILogger logger = Defaults.ServiceLocator.Instance.GetRequiredService<ILogger<CrawlerAgent>>();
        Dictionary<string, object> metadata = new(AgentMetadata)
        {
            [CrawlerAgentSettings.DefaultInputs.KamiYomuILogger] = logger
        };

        _crawler = GetCrawlerInstance(AssemblyPath, metadata);
        return _crawler;
    }

    public static ICrawlerAgent GetCrawlerInstance(string assemblyPath, IDictionary<string, object> options)
    {
        Assembly assembly = GetIsolatedAssembly(assemblyPath);
        return GetCrawlerInstance(assembly, options);
    }

    public static ICrawlerAgent GetCrawlerInstance(Assembly assembly, IDictionary<string, object> options)
    {
        string interfaceName = typeof(ICrawlerAgent).FullName!;

        Type crawlerType = assembly.GetTypes()
            .FirstOrDefault(t =>
                t.IsClass &&
                !t.IsAbstract &&
                t.GetInterfaces().Any(i => i.FullName == interfaceName))
            ?? throw new InvalidOperationException("No valid crawler type found.");

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

    public Dictionary<string, string> GetAssemblyMetadata()
    {
        _assembly ??= GetIsolatedAssembly(AssemblyPath);
        return GetAssemblyMetadata(_assembly);
    }

    public static string GetCrawlerDisplayName(Assembly assembly)
    {
        Type crawlerType = assembly.GetTypes()
            .FirstOrDefault(t =>
                t.IsClass &&
                !t.IsAbstract &&
                t.GetInterfaces().Any(i => i.FullName == typeof(ICrawlerAgent).FullName))
            ?? throw new InvalidOperationException("No valid crawler type found.");

        return crawlerType.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
            ?? assembly.FullName
            ?? "Agent";
    }

    public IEnumerable<AbstractInputAttribute> GetCrawlerInputs()
    {
        _assembly ??= GetIsolatedAssembly(AssemblyPath);
        return GetCrawlerInputs(_assembly);
    }

    public static IEnumerable<AbstractInputAttribute> GetCrawlerInputs(Assembly assembly)
    {
        Type crawlerType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(ICrawlerAgent).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            ?? throw new InvalidOperationException("No valid crawler type found.");

        List<AbstractInputAttribute> fields = [.. crawlerType.GetCustomAttributes<AbstractInputAttribute>(false)];

        fields.AddRange(
        [
            new CrawlerTextAttribute(CrawlerAgentSettings.DefaultInputs.BrowserUserAgent, I18n.UserAgentExplanation, true, CrawlerAgentSettings.HttpUserAgent, 900),
            new CrawlerTextAttribute(CrawlerAgentSettings.DefaultInputs.HttpClientTimeout, I18n.TimeoutExplanation, true, CrawlerAgentSettings.TimeoutMilliseconds.ToString(), 901),
        ]);

        return fields;
    }


    public void DeleteAssembly()
    {
        string dir = GetAgentDir(AssemblyName);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    public static Dictionary<string, string> GetAssemblyMetadata(Assembly assembly)
    {
        Dictionary<string, string> metadata = [];

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
    public static string GetAgentDir(string fileName)
    {
        IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();

        string name = GetAgentDirName(fileName);

        string directory = Path.Combine(specialFolderOptions.Value.AgentsDir, name);

        _ = Directory.CreateDirectory(directory);

        return directory;
    }

    public static string GetAgentDirName(string fileName)
    {
        return Path.GetFileNameWithoutExtension(fileName);
    }

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

    internal void Update(string? displayName, Dictionary<string, object> agentMetadata, Dictionary<string, string> assemblyProperties)
    {
        DisplayName = displayName;
        AgentMetadata = agentMetadata;
        AssemblyProperties = assemblyProperties;
    }

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

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
