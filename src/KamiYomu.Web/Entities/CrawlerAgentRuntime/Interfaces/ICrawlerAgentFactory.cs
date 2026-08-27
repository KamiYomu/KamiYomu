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
    ICrawlerAgentDecorator Create(string assemblyPath, Dictionary<string, object> metadata);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="crawlerAgent"></param>
    /// <returns></returns>
    ICrawlerAgentDecorator Create(CrawlerAgent crawlerAgent);
}
