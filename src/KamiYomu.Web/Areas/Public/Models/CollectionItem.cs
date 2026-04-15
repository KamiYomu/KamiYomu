
using KamiYomu.CrawlerAgents.Core.Catalog;

namespace KamiYomu.Web.Areas.Public.Models;

public class CollectionItem
{
    public Guid LibraryId { get; set; }
    public Guid CrawlerAgentId { get; set; }
    public Manga Manga { get; set; }

}
