using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using System.Text.RegularExpressions;

namespace KamiYomu.Web.Infrastructure.Repositories
{
    public class AgentCrawlerRepository(DbContext dbContext, CacheContext cacheContext) : IAgentCrawlerRepository
    {
        public Task<Manga> GetMangaAsync(CrawlerAgent agentCrawler, string mangaId, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{agentCrawler.Id}-manga-{mangaId}", async () =>
             {
                 var manga = await agentCrawler.GetCrawlerInstance().GetByIdAsync(mangaId.ToString(), cancellationToken);
                 return manga;
             }, TimeSpan.FromMinutes(30));
        }

        public Task<Manga> GetMangaAsync(Guid agentCrawlerId, string mangaId, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{agentCrawlerId}-manga-{mangaId}", async () =>
            {
                var agent = dbContext.CrawlerAgents.FindById(agentCrawlerId);
                var manga = await agent.GetCrawlerInstance().GetByIdAsync(mangaId.ToString(), cancellationToken);
                return manga;
            }, TimeSpan.FromMinutes(30));
        }

        public Task<PagedResult<Chapter>> GetMangaChaptersAsync(CrawlerAgent agentCrawler, string mangaId, PaginationOptions paginationOptions, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{agentCrawler.Id}-manga-{mangaId}-{paginationOptions}", async () =>
            {
                var library = dbContext.Libraries.Include(p => p.Manga).FindOne(p => p.Manga.Id == mangaId);
                return await agentCrawler.GetCrawlerInstance().GetChaptersAsync(library.Manga, paginationOptions, cancellationToken);
            }, TimeSpan.FromMinutes(30));
        }

        public Task<IEnumerable<Page>> GetChapterPagesAsync(CrawlerAgent agentCrawler, Chapter chapter, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{agentCrawler.Id}-chapter-{chapter.ParentManga.Id}-{chapter.Id}", async () =>
            {
                return await agentCrawler.GetCrawlerInstance().GetChapterPagesAsync(chapter, cancellationToken);
            }, TimeSpan.FromMinutes(30));
        }

        public Task<PagedResult<Manga>> SearchAsync(CrawlerAgent agentCrawler, string query, PaginationOptions paginationOptions, CancellationToken cancellationToken)
        {
            return cacheContext.GetOrSetAsync($"{agentCrawler.Id}-agent-{Regex.Replace(query, @"[^a-zA-Z0-9]", "")}-{paginationOptions}", async () =>
            {
                return await agentCrawler.GetCrawlerInstance().SearchAsync(query, paginationOptions, cancellationToken);
            }, TimeSpan.FromMinutes(5));
        }
    }
}
