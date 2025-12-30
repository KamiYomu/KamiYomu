using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

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
    public void OnGet()
    {
        CrawlerAgents = dbContext.CrawlerAgents.FindAll();
    }

    public async Task<IActionResult> OnPostAddToCollectionAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest("Invalid manga data.");
        }

        using CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(CrawlerAgentId);

        CrawlerAgents.Core.Catalog.Manga manga = await agentCrawlerRepository.GetMangaAsync(crawlerAgent.Id, MangaId, cancellationToken);

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

        UserPreference preferences = dbContext.UserPreferences.FindOne(p => true);
        preferences.SetFilePathTemplate(filePathTemplateFormat);
        preferences.SetComicInfoTitleTemplate(comicInfoTitleTemplateFormat);
        preferences.SetComicInfoSeriesTemplate(comicInfoSeriesTemplate);
        _ = dbContext.UserPreferences.Upsert(preferences);

        return Partial("_LibraryCard", library);
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

        return Partial("_LibraryCard", new Library(library.CrawlerAgent, library.Manga, null, null, null));
    }
}
