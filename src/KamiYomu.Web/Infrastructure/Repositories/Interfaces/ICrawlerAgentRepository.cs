using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;

namespace KamiYomu.Web.Infrastructure.Repositories.Interfaces
{
    public interface ICrawlerAgentRepository
    {
        Task<Manga> GetMangaAsync(Guid agentCrawlerId, string mangaId, CancellationToken cancellationToken);
        Task<Manga> GetMangaAsync(CrawlerAgent agentCrawler, string mangaId, CancellationToken cancellationToken);
        Task<PagedResult<Chapter>> GetMangaChaptersAsync(CrawlerAgent agentCrawler, string mangaId, PaginationOptions paginationOptions, CancellationToken cancellationToken);
        Task<IEnumerable<Page>> GetChapterPagesAsync(CrawlerAgent agentCrawler, Chapter chapter, CancellationToken cancellationToken);
        Task<PagedResult<Manga>> SearchAsync(CrawlerAgent agentCrawler, string query, PaginationOptions paginationOptions, CancellationToken cancellationToken);
    }
}
