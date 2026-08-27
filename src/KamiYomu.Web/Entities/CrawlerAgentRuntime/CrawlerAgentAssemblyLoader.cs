using System.ComponentModel;
using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Entities.CrawlerAgentRuntime;

public class CrawlerAgentAssemblyLoader : ICrawlerAgentAssemblyLoader
{
    /// <inheritdoc/>
    public Dictionary<string, string> GetAssemblyMetadata(CrawlerAgent crawlerAgent)
    {
        CrawlerAgentAssembly crawlerAgentAssembly = GetIsolatedAssembly(crawlerAgent.AssemblyPath);
        return GetAssemblyMetadata(crawlerAgentAssembly);
    }
    /// <inheritdoc/>
    public Dictionary<string, string> GetAssemblyMetadata(CrawlerAgentAssembly loadedAssembly)
    {
        ArgumentNullException.ThrowIfNull(loadedAssembly);

        Assembly assembly = loadedAssembly.Assembly;

        Dictionary<string, string> metadata = [];

        // File path
        metadata["FilePath"] = assembly.Location;

        // Title
        AssemblyTitleAttribute? titleAttribute =
            assembly.GetCustomAttribute<AssemblyTitleAttribute>();

        if (!string.IsNullOrWhiteSpace(titleAttribute?.Title))
        {
            metadata["Title"] = titleAttribute.Title;
        }

        // Description
        AssemblyDescriptionAttribute? descriptionAttribute =
            assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();

        if (!string.IsNullOrWhiteSpace(descriptionAttribute?.Description))
        {
            metadata["Description"] = descriptionAttribute.Description;
        }

        // Company
        AssemblyCompanyAttribute? companyAttribute =
            assembly.GetCustomAttribute<AssemblyCompanyAttribute>();

        if (!string.IsNullOrWhiteSpace(companyAttribute?.Company))
        {
            metadata["Company"] = companyAttribute.Company;
        }

        // Product
        AssemblyProductAttribute? productAttribute =
            assembly.GetCustomAttribute<AssemblyProductAttribute>();

        if (!string.IsNullOrWhiteSpace(productAttribute?.Product))
        {
            metadata["Product"] = productAttribute.Product;
        }

        // Assembly version
        string? version =
            assembly.GetName().Version?.ToString();

        if (!string.IsNullOrWhiteSpace(version))
        {
            metadata["Version"] = version;
        }

        // File version
        AssemblyFileVersionAttribute? fileVersionAttribute =
            assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();

        if (!string.IsNullOrWhiteSpace(fileVersionAttribute?.Version))
        {
            metadata["FileVersion"] = fileVersionAttribute.Version;
        }

        // Informational version
        AssemblyInformationalVersionAttribute? informationalVersionAttribute =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (!string.IsNullOrWhiteSpace(
                informationalVersionAttribute?.InformationalVersion))
        {
            metadata["InformationalVersion"] =
                informationalVersionAttribute.InformationalVersion;
        }

        return metadata;
    }

    /// <inheritdoc/>
    public CrawlerAgentAssembly GetIsolatedAssembly(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"Assembly file not found at path: {assemblyPath}");
        }

        CrawlerAgentLoadContext context = new(assemblyPath);

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
    /// <inheritdoc/>
    public ICrawlerAgentDecorator GetCrawlerInstance(Assembly assembly, IDictionary<string, object> options)
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
    /// <inheritdoc/>
    public string GetCrawlerDisplayName(Assembly assembly)
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
    /// <inheritdoc/>
    public IEnumerable<AbstractInputAttribute> GetCrawlerInputs(Assembly assembly)
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
    /// <inheritdoc/>
    public IEnumerable<AbstractInputAttribute> GetCrawlerInputs(CrawlerAgent crawlerAgent)
    {
       return GetCrawlerInputs(GetIsolatedAssembly(crawlerAgent.AssemblyPath).Assembly);
    }
    /// <inheritdoc/>
    public Version GetVersion(CrawlerAgent crawlerAgent)
    {
        Dictionary<string, string> metadata = GetAssemblyMetadata(crawlerAgent);

        return metadata.TryGetValue("Version", out string? version) &&
            Version.TryParse(version, out Version? parsedVersion)
            ? parsedVersion
            : new Version(0, 0, 0);
    }
}
