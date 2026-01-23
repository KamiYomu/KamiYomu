using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Contexts;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents;

public class IndexModel(DbContext dbContext) : PageModel
{
    public IEnumerable<Entities.CrawlerAgent>? CrawlerAgents { get; set; } = [];

    public void OnGet()
    {
        CrawlerAgents = dbContext.CrawlerAgents.FindAll().ToList();
    }
}
