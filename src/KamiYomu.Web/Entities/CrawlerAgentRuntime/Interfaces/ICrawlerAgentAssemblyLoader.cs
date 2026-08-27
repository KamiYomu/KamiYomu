using System.ComponentModel;
using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Inputs;

namespace KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;

/// <summary>
/// Assembly loader interface for dynamically loading and managing crawler agent assemblies in an isolated context.
/// </summary>
public interface ICrawlerAgentAssemblyLoader
{
    /// <summary>
    /// Extracts metadata from the specified assembly, including title, description, company, product, and version information.
    /// </summary>
    /// <param name="crawlerAgent">Crawler Agent</param>
    /// <returns>A dictionary containing assembly metadata with keys such as FilePath, Title, Description, Company, Product, Version, FileVersion, and InformationalVersion.</returns>
    Dictionary<string, string> GetAssemblyMetadata(CrawlerAgent crawlerAgent);
    /// <summary>
    /// Extracts metadata from the specified assembly, including title, description, company, product, and version information.
    /// </summary>
    /// <param name="loadedAssembly">The assembly to extract metadata from.</param>
    /// <returns>A dictionary containing assembly metadata with keys such as FilePath, Title, Description, Company, Product, Version, FileVersion, and InformationalVersion.</returns>
    Dictionary<string, string> GetAssemblyMetadata(CrawlerAgentAssembly loadedAssembly);
    /// <summary>
    /// Creates a new instance of the crawler agent from the assembly at the specified path.
    /// </summary>
    /// <param name="crawlerAgent">Crawler Agent</param>
    /// <returns>An instance of <see cref="ICrawlerAgentDecorator"/>.</returns>
    ICrawlerAgentDecorator GetCrawlerInstance(CrawlerAgent crawlerAgent);
    /// <summary>
    /// Creates a new instance of the crawler agent from the assembly at the specified path.
    /// </summary>
    /// <param name="assemblyPath">The file path to the crawler agent assembly.</param>
    /// <param name="options">A dictionary of options and dependencies to inject into the crawler instance constructor.</param>
    /// <returns>An instance of <see cref="ICrawlerAgentDecorator"/>.</returns>
    ICrawlerAgentDecorator GetCrawlerInstance(string assemblyPath, IDictionary<string, object> options);
    /// <summary>
    /// Loads an assembly from the specified path into an isolated <see cref="AssemblyLoadContext"/>.
    /// Validates that the assembly contains at least one non-abstract class implementing <see cref="ICrawlerAgent"/>.
    /// </summary>
    /// <param name="assemblyPath">The file path to the assembly to load.</param>
    /// <returns>The loaded assembly.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the assembly file does not exist at the specified path.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the assembly does not contain any non-abstract class implementing <see cref="ICrawlerAgent"/>.</exception>
    CrawlerAgentAssembly GetIsolatedAssembly(string assemblyPath);
    /// <summary>
    /// Creates a new instance of the crawler agent from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing the crawler agent implementation.</param>
    /// <param name="options">A dictionary of options and dependencies to inject into the crawler instance constructor.</param>
    /// <returns>An instance of <see cref="ICrawlerAgentDecorator"/> wrapped in a <see cref="ICrawlerAgentDecorator"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid crawler type is found in the assembly.</exception>
    /// <exception cref="InvalidCastException">Thrown when the created instance cannot be cast to <see cref="ICrawlerAgentDecorator"/>, typically due to assembly load context issues.</exception>
    ICrawlerAgentDecorator GetCrawlerInstance(Assembly assembly, IDictionary<string, object> options);
    /// <summary>
    /// Extracts the display name of the crawler agent from the specified assembly.
    /// Uses the <see cref="DisplayNameAttribute"/> if available, otherwise falls back to the assembly full name or "Agent".
    /// </summary>
    /// <param name="assembly">The assembly containing the crawler agent implementation.</param>
    /// <returns>The display name of the crawler agent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid crawler type is found in the assembly.</exception>
    string GetCrawlerDisplayName(Assembly assembly);
    /// <summary>
    /// Retrieves all input attributes defined on the crawler agent type in the specified assembly.
    /// Includes default system inputs for user agent and timeout configuration.
    /// </summary>
    /// <param name="assembly">The assembly containing the crawler agent implementation.</param>
    /// <returns>An enumerable collection of <see cref="AbstractInputAttribute"/> objects including both custom and default inputs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid crawler type is found in the assembly.</exception>
    IEnumerable<AbstractInputAttribute> GetCrawlerInputs(Assembly assembly);
    /// <summary>
    /// Retrieves all input attributes defined on the crawler agent type in the specified assembly.
    /// Includes default system inputs for user agent and timeout configuration.
    /// </summary>
    /// <param name="crawlerAgent">Crawler Agent.</param>
    /// <returns>An enumerable collection of <see cref="AbstractInputAttribute"/> objects including both custom and default inputs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid crawler type is found in the assembly.</exception>
    IEnumerable<AbstractInputAttribute> GetCrawlerInputs(CrawlerAgent crawlerAgent);
    /// <summary>
    /// Get Folder version from the assembly name. It extracts the version number from the assembly name using a regular expression.
    /// </summary>
    /// <returns>The version number as a string in the format "major.minor.patch". If no version is found, returns "0.0.0".</returns>
    Version GetVersion(CrawlerAgent crawlerAgent);
}
