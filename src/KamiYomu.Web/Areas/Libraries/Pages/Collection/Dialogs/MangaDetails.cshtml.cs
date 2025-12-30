using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

public class MangaDetailsModel(ICrawlerAgentRepository crawlerAgentRepository) : PageModel
{
    public Manga? Manga { get; set; } = default;
    public async Task OnGetAsync(Guid crawlerAgentId, string mangaId, CancellationToken cancellationToken)
    {
        if (crawlerAgentId == Guid.Empty || string.IsNullOrWhiteSpace(mangaId))
        {
            return;
        }

        Manga = await crawlerAgentRepository.GetMangaAsync(crawlerAgentId, mangaId, cancellationToken);
    }
}
