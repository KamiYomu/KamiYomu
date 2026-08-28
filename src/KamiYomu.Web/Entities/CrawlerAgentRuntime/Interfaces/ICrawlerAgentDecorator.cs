namespace KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
/// <summary>
/// Group all the interfaces of a CrawlerAgent and a DefaultHeadersCrawlerAgent into one interface.
/// </summary>
public interface ICrawlerAgentDecorator : ICrawlerAgent, IDefaultHeadersCrawlerAgent
{
}
