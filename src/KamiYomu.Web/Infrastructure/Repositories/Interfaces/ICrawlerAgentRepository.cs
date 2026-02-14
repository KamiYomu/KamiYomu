using KamiYomu.CrawlerAgents.Core.Catalog;

namespace KamiYomu.Web.Infrastructure.Repositories.Interfaces;

public interface ICrawlerAgentRepository
{
    Task<Manga> GetMangaAsync(Guid crawlerAgentId, string mangaId, CancellationToken cancellationToken);
    Task<PagedResult<Chapter>> GetMangaChaptersAsync(Guid crawlerAgentId, string mangaId, PaginationOptions paginationOptions, CancellationToken cancellationToken);
    Task<IEnumerable<Page>> GetChapterPagesAsync(Guid crawlerAgentId, Chapter chapter, CancellationToken cancellationToken);
    Task<PagedResult<Manga>> SearchAsync(Guid crawlerAgentId, string query, PaginationOptions paginationOptions, CancellationToken cancellationToken);
}
