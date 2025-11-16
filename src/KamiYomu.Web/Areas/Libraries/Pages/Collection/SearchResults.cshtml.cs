using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Mangas
{
    public class SearchResultsModel(DbContext dbContext, IAgentCrawlerRepository agentCrawlerRepository) : PageModel
    {
        public IEnumerable<Entities.Library> Results { get; set; } = [];

        public async Task<IActionResult> OnGetCrawlerAsync(string query, Guid selectedAgent, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new EmptyResult();

            var agent = dbContext.CrawlerAgents.FindOne(p => p.Id == selectedAgent);
            if (agent == null)
            {
                return new EmptyResult();
            }
            var userPreference = dbContext.UserPreferences.FindOne(p => true);
            var queryResult = await agentCrawlerRepository.SearchAsync(agent, query, new PaginationOptions(0, 30), cancellationToken);
            Results = queryResult.Data.Where(p => p.IsFamilySafe == true || p.IsFamilySafe == userPreference.FamilySafeMode).Select(p => new Entities.Library(agent, p));
            ViewData["ShowAddToLibrary"] = true;
            return Page();
        }

        public IActionResult OnGetSearch(string query)
        {
            var userPreference = dbContext.UserPreferences.FindOne(p => true);
            Results = [.. dbContext.Libraries.Include(p => p.AgentCrawler)
                                             .Find(p => (query == string.Empty || p.Manga.Title.Contains(query))
                                               && (p.Manga.IsFamilySafe == true || p.Manga.IsFamilySafe == userPreference.FamilySafeMode))];
            ViewData["ShowAddToLibrary"] = false;
            return Page();
        }

    }
}
