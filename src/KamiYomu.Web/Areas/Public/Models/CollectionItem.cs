
using KamiYomu.CrawlerAgents.Core.Catalog;

namespace KamiYomu.Web.Areas.Public.Models;

public class CollectionItem
{
    public Guid LibraryId { get; internal set; }
    public Guid CrawlerAgentId { get; internal set; }
    public Manga Manga { get; internal set; }

}
