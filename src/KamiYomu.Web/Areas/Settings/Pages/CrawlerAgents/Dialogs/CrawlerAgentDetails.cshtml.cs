using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Dialogs;

public class AgentDetailsModel(DbContext agentDbContext) : PageModel
{
    public required Entities.CrawlerAgent Agent { get; set; }
    public void OnGet(Guid id)
    {
        Agent = agentDbContext.CrawlerAgents.FindById(id);
    }
}
