using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PuppeteerSharp;

namespace KamiYomu.Web.Areas.Libraries.Pages.Mangas
{
    public class SearchResultsModel(DbContext dbContext, ICrawlerAgentRepository agentCrawlerRepository) : PageModel
    {
        public IEnumerable<Entities.Library> Results { get; set; } = [];

        public async Task<IActionResult> OnGetCrawlerAsync(
            string query, 
            Guid selectedAgent, 
            int offset = 0, 
            int limit = 30, 
            string continuationToken = "", 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new EmptyResult();

            var crawlerAgent = dbContext.CrawlerAgents.FindOne(p => p.Id == selectedAgent);
            if (crawlerAgent == null)
            {
                return new EmptyResult();
            }
            var userPreference = dbContext.UserPreferences.FindOne(p => true);
            var paginationOptions = !string.IsNullOrWhiteSpace(continuationToken) ? new PaginationOptions(continuationToken) : new PaginationOptions(offset, 30);
            var queryResult = await agentCrawlerRepository.SearchAsync(crawlerAgent.Id, query, paginationOptions, cancellationToken);
            Results = queryResult.Data.Where(p => p.IsFamilySafe == true || p.IsFamilySafe == userPreference.FamilySafeMode).Select(p => new Entities.Library(crawlerAgent, p));
            ViewData["ShowAddToLibrary"] = true;
            ViewData["Handler"] = "Crawler";
            ViewData[nameof(query)] = query;
            ViewData[nameof(selectedAgent)] = selectedAgent;
            ViewData[nameof(PaginationOptions.OffSet)] = queryResult.PaginationOptions.OffSet + queryResult.PaginationOptions.Limit;
            ViewData[nameof(PaginationOptions.Limit)] = queryResult.PaginationOptions.Limit;
            ViewData[nameof(PaginationOptions.ContinuationToken)] = queryResult.PaginationOptions.ContinuationToken;
            return Page();
        }

        public IActionResult OnGetSearch(
            string query,
            int offset = 0,
            int limit = 30,
            string continuationToken = "",
            CancellationToken cancellationToken = default)
        {
            var userPreference = dbContext.UserPreferences.FindOne(p => true);
            Results = [.. dbContext.Libraries.Include(p => p.CrawlerAgent)
                                             .Find(p => (query == string.Empty || p.Manga.Title.Contains(query))
                                               && (p.Manga.IsFamilySafe == true || p.Manga.IsFamilySafe == userPreference.FamilySafeMode))
                                             .Skip(offset)
                                             .Take(limit)];
            ViewData["ShowAddToLibrary"] = false;
            ViewData["Handler"] = "Search";
            ViewData[nameof(query)] = query;
            ViewData[nameof(PaginationOptions.OffSet)] = offset + limit;
            ViewData[nameof(PaginationOptions.Limit)] = limit;
            ViewData[nameof(PaginationOptions.ContinuationToken)] = string.Empty;
            return Page();
        }

    }
}
