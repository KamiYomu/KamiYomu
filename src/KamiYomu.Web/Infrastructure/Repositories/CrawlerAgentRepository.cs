using System.Text.RegularExpressions;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

namespace KamiYomu.Web.Infrastructure.Repositories;
/// <summary>
/// CrawlerAgentRepository is responsible for interacting with crawler agents to retrieve manga, 
/// chapters, and pages, as well as performing search operations. 
/// It utilizes caching to improve performance and reduce redundant requests.
/// </summary>
/// <param name="dbContext"></param>
/// <param name="cacheContext"></param>
/// <param name="crawlerAgentAssemblyLoader"></param>
public class CrawlerAgentRepository(
    DbContext dbContext,
    CacheContext cacheContext,
    ICrawlerAgentAssemblyLoader crawlerAgentAssemblyLoader) : ICrawlerAgentRepository
{
    ///<inheritdoc/>
    public Task<Manga> GetMangaAsync(Guid crawlerAgentId, string mangaId, CancellationToken cancellationToken)
    {
        return cacheContext.GetOrSetAsync($"{crawlerAgentId}-manga-{mangaId}", async () =>
        {
            CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(crawlerAgentId);
            using ICrawlerAgentDecorator crawlerInstance = crawlerAgentAssemblyLoader.GetCrawlerInstance(crawlerAgent);
            Manga manga = await crawlerInstance.GetByIdAsync(mangaId.ToString(), cancellationToken);
            return manga;
        }, TimeSpan.FromMinutes(30));
    }

    ///<inheritdoc/>
    public Task<PagedResult<Chapter>> GetMangaChaptersAsync(Guid crawlerAgentId, string mangaId, PaginationOptions paginationOptions, CancellationToken cancellationToken)
    {
        return cacheContext.GetOrSetAsync($"{crawlerAgentId}-manga-{mangaId}-{paginationOptions}", async () =>
        {
            CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(crawlerAgentId);
            Library library = dbContext.Libraries.Include(p => p.Manga).FindOne(p => p.Manga.Id == mangaId);
            using ICrawlerAgentDecorator crawlerInstance = crawlerAgentAssemblyLoader.GetCrawlerInstance(crawlerAgent);
            return await crawlerInstance.GetChaptersAsync(library.Manga, paginationOptions, cancellationToken);
        }, TimeSpan.FromMinutes(30));
    }

    ///<inheritdoc/>
    public Task<IEnumerable<Page>> GetChapterPagesAsync(Guid crawlerAgentId, Chapter chapter, CancellationToken cancellationToken)
    {
        return cacheContext.GetOrSetAsync($"{crawlerAgentId}-chapter-{chapter.ParentManga.Id}-{chapter.Id}", async () =>
        {
            CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(crawlerAgentId);
            using ICrawlerAgentDecorator crawlerInstance = crawlerAgentAssemblyLoader.GetCrawlerInstance(crawlerAgent);
            return await crawlerInstance.GetChapterPagesAsync(chapter, cancellationToken);
        }, TimeSpan.FromMinutes(30));
    }

    ///<inheritdoc/>
    public Task<PagedResult<Manga>> SearchAsync(Guid crawlerAgentId, string query, PaginationOptions paginationOptions, CancellationToken cancellationToken)
    {
        return cacheContext.GetOrSetAsync($"{crawlerAgentId}-agent-{Regex.Replace(query, @"[^a-zA-Z0-9]", "")}-{paginationOptions}", async () =>
        {
            CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(crawlerAgentId);
            using ICrawlerAgentDecorator crawlerInstance = crawlerAgentAssemblyLoader.GetCrawlerInstance(crawlerAgent);
            return await crawlerInstance.SearchAsync(query, paginationOptions, cancellationToken);
        }, TimeSpan.FromMinutes(5));
    }
}
