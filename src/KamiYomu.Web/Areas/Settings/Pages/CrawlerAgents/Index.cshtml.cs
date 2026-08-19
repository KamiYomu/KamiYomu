using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc.RazorPages;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents;

public class IndexModel([FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext) : PageModel
{
    public IEnumerable<Entities.CrawlerAgent>? CrawlerAgents { get; set; } = [];

    public void OnGet()
    {
        CrawlerAgents = dbContext.CrawlerAgents.FindAll();
    }
}
