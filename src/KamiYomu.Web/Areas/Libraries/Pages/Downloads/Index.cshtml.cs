using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Downloads;

public class IndexModel(
    DbContext dbContext,
    IDownloadAppService downloadAppService,
    ICrawlerAgentRepository agentCrawlerRepository) : PageModel
{
    public IEnumerable<CrawlerAgent> CrawlerAgents { get; set; } = [];

    public IEnumerable<Library> Results { get; set; } = [];

    [BindProperty]
    public required string MangaId { get; set; }

    [BindProperty]
    public Guid CrawlerAgentId { get; set; }

    [BindProperty]
    public string FilePathTemplate { get; set; } = string.Empty;

    [BindProperty]
    public required string ComicInfoTitleTemplate { get; set; }

    [BindProperty]
    public required string ComicInfoSeriesTemplate { get; set; }
    [BindProperty]
    public required bool MakeThisConfigurationDefault { get; set; } = false;

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public Guid? SelectedAgent { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Offset { get; set; } = 0;

    [BindProperty(SupportsGet = true)]
    public int? Limit { get; set; } = 30;

    [BindProperty(SupportsGet = true)]
    public string? ContinuationToken { get; set; } = string.Empty;

    public void OnGet()
    {
        CrawlerAgents = dbContext.CrawlerAgents.FindAll();
    }

    public async Task<IActionResult> OnGetSearchAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return new EmptyResult();
        }

        CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(SelectedAgent);
        if (crawlerAgent == null)
        {
            return new EmptyResult();
        }

        UserPreference userPreference = dbContext.UserPreferences.Query().FirstOrDefault();
        PaginationOptions paginationOptions = !string.IsNullOrWhiteSpace(ContinuationToken) ? new PaginationOptions(ContinuationToken) : new PaginationOptions(Offset, 30);
        PagedResult<Manga> queryResult = await agentCrawlerRepository.SearchAsync(crawlerAgent.Id, Query, paginationOptions, cancellationToken);
        Results = queryResult.Data.Where(p => p.IsFamilySafe || p.IsFamilySafe == userPreference.FamilySafeMode).Select(p => new Library(crawlerAgent, p, null, null, null));
        ViewData["ShowAddToLibrary"] = true;
        ViewData["Handler"] = "Crawler";
        ViewData[nameof(Query)] = Query;
        ViewData[nameof(SelectedAgent)] = SelectedAgent;
        ViewData[nameof(PaginationOptions.OffSet)] = queryResult.PaginationOptions.OffSet + queryResult.PaginationOptions.Limit;
        ViewData[nameof(PaginationOptions.Limit)] = queryResult.PaginationOptions.Limit;
        ViewData[nameof(PaginationOptions.ContinuationToken)] = queryResult.PaginationOptions.ContinuationToken;

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return ViewComponent("SearchMangaResult", new
            {
                libraries = Results,
                searchUri = Url.Page("/Downloads/Index", new
                {
                    Area = "Libraries",
                    Handler = "Search",
                    Query = ViewData[nameof(Query)],
                    SelectedAgent = ViewData[nameof(SelectedAgent)],
                    OffSet = ViewData[nameof(Offset)],
                    Limit = ViewData[nameof(Limit)],
                    ContinuationToken = ViewData[nameof(ContinuationToken)]
                })
            });
        }

        CrawlerAgents = dbContext.CrawlerAgents.FindAll();

        return Page();
    }


    public async Task<IActionResult> OnPostAddToCollectionAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest("Invalid manga data.");
        }

        Library library = await downloadAppService.AddToCollectionAsync(new AddItemCollection
        {
            ComicInfoSeriesTemplate = ComicInfoSeriesTemplate,
            ComicInfoTitleTemplate = ComicInfoTitleTemplate,
            CrawlerAgentId = CrawlerAgentId,
            FilePathTemplate = FilePathTemplate,
            MakeThisConfigurationDefault = MakeThisConfigurationDefault,
            MangaId = MangaId
        }, cancellationToken);

        return ViewComponent("LibraryCard", new Dictionary<string, object>
        {
            { "library", library },
            { nameof(cancellationToken), cancellationToken }
        });
    }

    public async Task<IActionResult> OnPostRemoveFromCollectionAsync(CancellationToken cancellationToken)
    {
        _ = ModelState.Remove(nameof(FilePathTemplate));
        _ = ModelState.Remove(nameof(ComicInfoTitleTemplate));
        _ = ModelState.Remove(nameof(ComicInfoSeriesTemplate));

        if (!ModelState.IsValid)
        {
            return BadRequest("Invalid manga data.");
        }

        Library library = await downloadAppService.RemoveFromCollectionAsync(new RemoveItemCollection
        {
            CrawlerAgentId = CrawlerAgentId,
            MangaId = MangaId
        }, cancellationToken);

        return ViewComponent("LibraryCard", new Dictionary<string, object>
        {
            { "library", new Library(library.CrawlerAgent, library.Manga, null, null, null) },
            { nameof(cancellationToken), cancellationToken }
        });
    }
}
