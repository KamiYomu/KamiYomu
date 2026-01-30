using System.Reflection;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using LiteDB;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

using PuppeteerSharp;

namespace KamiYomu.Web.Areas.Libraries.Pages.Downloads;

public class IndexModel(
    ILogger<IndexModel> logger,
    IOptions<SpecialFolderOptions> specialFolderOptions,
    DbContext dbContext,
    ICrawlerAgentRepository agentCrawlerRepository,
    IWorkerService workerService,
    INotificationService notificationService) : PageModel
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

        using CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(CrawlerAgentId);

        Manga manga = await agentCrawlerRepository.GetMangaAsync(crawlerAgent.Id, MangaId, cancellationToken);

        string filePathTemplateFormat = string.IsNullOrWhiteSpace(FilePathTemplate) ? specialFolderOptions.Value.FilePathFormat : FilePathTemplate;
        string comicInfoTitleTemplateFormat = string.IsNullOrWhiteSpace(ComicInfoTitleTemplate) ? specialFolderOptions.Value.ComicInfoTitleFormat : ComicInfoTitleTemplate;
        string comicInfoSeriesTemplate = string.IsNullOrWhiteSpace(ComicInfoSeriesTemplate) ? specialFolderOptions.Value.ComicInfoSeriesFormat : ComicInfoSeriesTemplate;

        Library library = new(crawlerAgent, manga, filePathTemplateFormat, comicInfoTitleTemplateFormat, comicInfoSeriesTemplate);

        _ = dbContext.Libraries.Insert(library);

        MangaDownloadRecord downloadRecord = new(library, string.Empty);

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        _ = libDbContext.MangaDownloadRecords.Insert(downloadRecord);

        string backgroundJobId = workerService.ScheduleMangaDownload(downloadRecord);

        downloadRecord.Schedule(backgroundJobId);

        _ = libDbContext.MangaDownloadRecords.Update(downloadRecord);

        await notificationService.PushSuccessAsync($"{I18n.TitleAddedToYourCollection}: {library.Manga.Title} ", cancellationToken);

        if (MakeThisConfigurationDefault)
        {
            UserPreference preferences = dbContext.UserPreferences.FindOne(p => true);
            preferences.SetFilePathTemplate(filePathTemplateFormat);
            preferences.SetComicInfoTitleTemplate(comicInfoTitleTemplateFormat);
            preferences.SetComicInfoSeriesTemplate(comicInfoSeriesTemplate);
            _ = dbContext.UserPreferences.Upsert(preferences);
        }


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

        Library library = dbContext.Libraries.Include(p => p.Manga)
                                         .Include(p => p.CrawlerAgent)
                                         .FindOne(p => p.Manga.Id == MangaId && p.CrawlerAgent.Id == CrawlerAgentId);
        string mangaTitle = library.Manga.Title;

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        MangaDownloadRecord mangaDownload = libDbContext.MangaDownloadRecords.Include(p => p.Library).FindOne(p => p.Library.Id == library.Id);

        if (mangaDownload != null)
        {
            workerService.CancelMangaDownload(mangaDownload);
        }

        library.DropDbContext();

        _ = dbContext.Libraries.Delete(library.Id);

        logger.LogInformation("Drop Database {database}", libDbContext.DatabaseFilePath());

        await notificationService.PushSuccessAsync($"{I18n.YourCollectionNoLongerIncludes}: {mangaTitle}.", cancellationToken);

        return ViewComponent("LibraryCard", new Dictionary<string, object>
        {
            { "library", new Library(library.CrawlerAgent, library.Manga, null, null, null) },
            { nameof(cancellationToken), cancellationToken }
        });
    }
}
