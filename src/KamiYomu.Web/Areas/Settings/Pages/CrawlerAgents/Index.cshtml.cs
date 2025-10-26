using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents
{
    public class IndexModel(DbContext agentDbContext) : PageModel
    {
        public IEnumerable<Entities.CrawlerAgent>? CrawlerAgents { get; set; } = [];

        public void OnGet()
        {
            CrawlerAgents = agentDbContext.CrawlerAgents.FindAll().ToList();
        }
    }
}
