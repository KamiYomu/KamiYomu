using KamiYomu.CrawlerAgents.Core.Catalog;

namespace KamiYomu.Web.Infrastructure.Repositories.Interfaces;
/// <summary>
/// CrawlerAgentRepository defines the contract for interacting with 
/// crawler agents to retrieve manga, chapters, and pages, as well as performing search operations.
/// </summary>
public interface ICrawlerAgentRepository
{
    /// <summary>
    /// Gets the manga information for a specific manga ID from a specified
    /// </summary>
    /// <param name="crawlerAgentId"></param>
    /// <param name="mangaId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Manga> GetMangaAsync(Guid crawlerAgentId, string mangaId, CancellationToken cancellationToken);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="crawlerAgentId"></param>
    /// <param name="mangaId"></param>
    /// <param name="paginationOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<PagedResult<Chapter>> GetMangaChaptersAsync(Guid crawlerAgentId, string mangaId, PaginationOptions paginationOptions, CancellationToken cancellationToken);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="crawlerAgentId"></param>
    /// <param name="chapter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<Page>> GetChapterPagesAsync(Guid crawlerAgentId, Chapter chapter, CancellationToken cancellationToken);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="crawlerAgentId"></param>
    /// <param name="query"></param>
    /// <param name="paginationOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<PagedResult<Manga>> SearchAsync(Guid crawlerAgentId, string query, PaginationOptions paginationOptions, CancellationToken cancellationToken);
}
