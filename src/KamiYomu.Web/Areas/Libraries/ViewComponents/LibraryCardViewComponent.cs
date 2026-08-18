using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Libraries.ViewComponents;

public class LibraryCardViewComponent([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Library library, CancellationToken cancellationToken = default)
    {
        CrawlerAgent? crawlerAgent = dbContext.CrawlerAgents.FindById(library.CrawlerAgent.Id);
        Uri faviconUrl = new("/images/favicon.ico", UriKind.Relative);
        bool crawlerAgentDisabled = true;
        if (crawlerAgent != null)
        {
            using ICrawlerAgent crawlerInstance = library.CrawlerAgent.GetCrawlerInstance();
            faviconUrl = await crawlerInstance.GetFaviconAsync(cancellationToken);
            crawlerAgentDisabled = false;
        }

        bool isNew = library.Id == Guid.Empty;
        string cardId = $"library-card-{library.Manga.Id}".Replace(".", "-");
        string addToCollectionUrl = $"/Libraries/Collection/Dialogs/AddToCollection?CrawlerAgentId={library.CrawlerAgent.Id}&MangaId={library.Manga.Id}&RefreshElementId={cardId}";
        string removeFromCollectionUrl = $"/Libraries/Collection/Dialogs/RemoveFromCollection?LibraryId={library.Id}&RefreshElementId={cardId}";
        string downloadStatusUrl = $"/Libraries/Collection/Dialogs/DownloadStatus?libraryId={library.Id}";
        string mangaDetailsUrl = $"/Libraries/Collection/Dialogs/MangaDetails?crawlerAgentId={library.CrawlerAgent.Id}&mangaId={library.Manga.Id}";
        string upgradeCrawlerAgentUrl = $"/Libraries/Collection/Dialogs/UpgradeCrawlerAgent?libraryId={library.Id}&refreshElementId={cardId}";

        return View(
            new LibraryCardViewComponentModel(
                library,
                faviconUrl,
                isNew,
                cardId,
                addToCollectionUrl,
                removeFromCollectionUrl,
                downloadStatusUrl,
                mangaDetailsUrl,
                upgradeCrawlerAgentUrl,
                crawlerAgentDisabled));
    }
}

public record LibraryCardViewComponentModel(
    Library Library,
    Uri FaviconUrl,
    bool IsNew,
    string CardId,
    string AddToCollectionUrl,
    string RemoveFromCollectionUrl,
    string DownloadStatusUrl,
    string MangaDetailsUrl,
    string UpgradeCrawlerAgentUrl,
    bool CrawlerAgentDisabled);
