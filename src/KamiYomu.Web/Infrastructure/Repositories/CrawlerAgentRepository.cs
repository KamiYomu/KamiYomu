using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using System.Text.RegularExpressions;

namespace KamiYomu.Web.Infrastructure.Repositories
{
    public class CrawlerAgentRepository(DbContext dbContext, CacheContext cacheContext) : ICrawlerAgentRepository
    {
        public Task<Manga> GetMangaAsync(Guid crawlerAgentId, string mangaId, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{crawlerAgentId}-manga-{mangaId}", async () =>
            {
                using var agentCrawler = dbContext.CrawlerAgents.FindById(crawlerAgentId);
                using var crawlerInstance = agentCrawler.GetCrawlerInstance();
                var manga = await crawlerInstance.GetByIdAsync(mangaId.ToString(), cancellationToken);
                return manga;
            }, TimeSpan.FromMinutes(30));
        }

        public Task<PagedResult<Chapter>> GetMangaChaptersAsync(Guid crawlerAgentId, string mangaId, PaginationOptions paginationOptions, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{crawlerAgentId}-manga-{mangaId}-{paginationOptions}", async () =>
            {
                using var agentCrawler = dbContext.CrawlerAgents.FindById(crawlerAgentId);
                var library = dbContext.Libraries.Include(p => p.Manga).FindOne(p => p.Manga.Id == mangaId);
                using var crawlerInstance = agentCrawler.GetCrawlerInstance();
                return await crawlerInstance.GetChaptersAsync(library.Manga, paginationOptions, cancellationToken);
            }, TimeSpan.FromMinutes(30));
        }

        public Task<IEnumerable<Page>> GetChapterPagesAsync(Guid crawlerAgentId, Chapter chapter, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{crawlerAgentId}-chapter-{chapter.ParentManga.Id}-{chapter.Id}", async () =>
            {
                using var agentCrawler = dbContext.CrawlerAgents.FindById(crawlerAgentId);
                using var crawlerInstance = agentCrawler.GetCrawlerInstance();
                return await crawlerInstance.GetChapterPagesAsync(chapter, cancellationToken);
            }, TimeSpan.FromMinutes(30));
        }

        public Task<PagedResult<Manga>> SearchAsync(Guid crawlerAgentId, string query, PaginationOptions paginationOptions, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{crawlerAgentId}-agent-{Regex.Replace(query, @"[^a-zA-Z0-9]", "")}-{paginationOptions}", async () =>
            {
                using var agentCrawler = dbContext.CrawlerAgents.FindById(crawlerAgentId);
                using var crawlerInstance = agentCrawler.GetCrawlerInstance();
                return await crawlerInstance.SearchAsync(query, paginationOptions, cancellationToken);
            }, TimeSpan.FromMinutes(5));
        }
    }
}
