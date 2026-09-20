using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class CollectionItemTests
{
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        Guid libraryId = Guid.NewGuid();
        Guid crawlerAgentId = Guid.NewGuid();
        var manga = MangaBuilder.Create().WithTitle("Test Manga").Build();

        CollectionItem item = new()
        {
            LibraryId = libraryId,
            CrawlerAgentId = crawlerAgentId,
            Manga = manga
        };

        Assert.Equal(libraryId, item.LibraryId);
        Assert.Equal(crawlerAgentId, item.CrawlerAgentId);
        Assert.Same(manga, item.Manga);
    }
}
