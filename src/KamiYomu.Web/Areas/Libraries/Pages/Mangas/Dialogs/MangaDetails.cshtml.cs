using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Mangas.Dialogs
{
    public class MangaDetailsModel(DbContext agentDbContext, CacheContext cacheContext, IAgentCrawlerRepository agentCrawlerRepository) : PageModel
    {
        public Manga? Manga { get; set; } = default;
        public async Task OnGetAsync(Guid agentId, string mangaId, CancellationToken cancellationToken)
        {
            if(agentId == Guid.Empty || string.IsNullOrWhiteSpace(mangaId))
            {
                return;
            }

            Manga = await agentCrawlerRepository.GetMangaAsync(agentId, mangaId, cancellationToken);
        }
    }
}
