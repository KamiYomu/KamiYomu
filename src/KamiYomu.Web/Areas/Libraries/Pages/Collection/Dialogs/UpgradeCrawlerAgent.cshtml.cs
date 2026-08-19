using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

public class UpgradeCrawlerAgentModel(
    DbContext dbContext,
    INotificationService notificationService,
    ICrawlerAgentAppService crawlerAgentAppService) : PageModel
{
    public Guid LibraryId { get; set; }
    public required string RefreshElementId { get; set; }
    public IEnumerable<CrawlerAgent> AvailableVersions { get; set; }

    public required Library Library { get; set; }
    public void OnGet(Guid libraryId, string refreshElementId)
    {
        RefreshElementId = refreshElementId;
        LibraryId = libraryId;
        Library = dbContext.Libraries.Include(p => p.Manga).Include(p => p.CrawlerAgent).FindOne(p => p.Id == LibraryId);
        AvailableVersions = dbContext.CrawlerAgents.Query().Where(p => p.AssemblyName == Library.CrawlerAgent.AssemblyName).ToList();
    }

    public async Task<IActionResult> OnPostUpgradeCrawlerAgentAsync(Guid libraryId, Guid crawlerAgentId, CancellationToken cancellationToken)
    {

        Library library = await crawlerAgentAppService.UpgradeCrawlerAgentAsync(libraryId, crawlerAgentId, cancellationToken);

        if (library != null && library.CrawlerAgent.Id == crawlerAgentId)
        {
            await notificationService.PushSuccessAsync(I18n.CrawlerAgentHasBeenUpgraded, cancellationToken);
        }

        return ViewComponent("LibraryCard", new Dictionary<string, object>
        {
            { "library", library },
            { nameof(cancellationToken), cancellationToken }
        });
    }
}
