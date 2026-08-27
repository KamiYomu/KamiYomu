using System.Reflection;

namespace KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
///
public interface ICrawlerAgentFactory
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="assemblyPath"></param>
    /// <param name="metadata"></param>
    /// <returns></returns>
    ICrawlerAgentDecorator Create(string assemblyPath, IDictionary<string, object> metadata);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="crawlerAgent"></param>
    /// <returns></returns>
    ICrawlerAgentDecorator Create(CrawlerAgent crawlerAgent);
    /// <summary>
    /// Creates a new instance of the crawler agent from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing the crawler agent implementation.</param>
    /// <param name="options">A dictionary of options and dependencies to inject into the crawler instance constructor.</param>
    /// <returns>An instance of <see cref="ICrawlerAgentDecorator"/> wrapped in a <see cref="ICrawlerAgentDecorator"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no valid crawler type is found in the assembly.</exception>
    /// <exception cref="InvalidCastException">Thrown when the created instance cannot be cast to <see cref="ICrawlerAgentDecorator"/>, typically due to assembly load context issues.</exception>
    ICrawlerAgentDecorator Create(Assembly assembly, IDictionary<string, object> options);
}
