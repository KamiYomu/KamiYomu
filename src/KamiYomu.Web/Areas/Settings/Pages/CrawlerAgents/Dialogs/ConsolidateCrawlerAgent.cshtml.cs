using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Dialogs;

public class ConsolidateCrawlerAgentModel(
    [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext,
    ICrawlerAgentAppService crawlerAgentAppService,
    INotificationService notificationService) : PageModel
{
    [BindProperty]
    public Guid CrawlerAgentId { get; set; }

    [BindProperty]
    public int LibrariesUsingThisCrawlerAgent { get; set; }
    public CrawlerAgent CrawlerAgent { get; private set; }

    public void OnGet(Guid id)
    {
        CrawlerAgentId = id;
        CrawlerAgent = dbContext.CrawlerAgents.FindById(CrawlerAgentId);
        ILiteQueryable<Library> query = dbContext.Libraries.Query();
        LibrariesUsingThisCrawlerAgent = query.Where(p => p.CrawlerAgent.AssemblyName == CrawlerAgent.AssemblyName).Count();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(CrawlerAgentId);

        _ = await crawlerAgentAppService.ConsolidateCollectionByAssemblyNameAsync(crawlerAgent, cancellationToken);

        List<CrawlerAgent> crawlerAgents = [.. dbContext.CrawlerAgents.FindAll()];

        await notificationService.PushSuccessAsync(I18n.AllLibrariesSharedSameCrawlerAgentSuccessfully, cancellationToken);

        return Partial("_CrawlerAgentList", crawlerAgents);
    }
}
