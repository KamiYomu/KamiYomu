using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Libraries.Pages.Download
{
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
        public string MangaId { get; set; }

        [BindProperty]
        public Guid CrawlerAgentId { get; set; }

        [BindProperty]
        public string FilePathTemplate { get; set; } = string.Empty;
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
            
            using var crawlerAgent = dbContext.CrawlerAgents.FindById(CrawlerAgentId);

            var manga = await agentCrawlerRepository.GetMangaAsync(crawlerAgent.Id, MangaId, cancellationToken);

            var filePathTemplateFormat = string.IsNullOrWhiteSpace(FilePathTemplate) ? specialFolderOptions.Value.FilePathFormat : FilePathTemplate;
            
            var library = new Library(crawlerAgent, manga, filePathTemplateFormat);

            dbContext.Libraries.Insert(library);

            var downloadRecord = new MangaDownloadRecord(library, string.Empty);

            using var libDbContext = library.GetDbContext();

            libDbContext.MangaDownloadRecords.Insert(downloadRecord);

            string backgroundJobId = workerService.ScheduleMangaDownload(downloadRecord);

            downloadRecord.Schedule(backgroundJobId);

            libDbContext.MangaDownloadRecords.Update(downloadRecord);

            await notificationService.PushSuccessAsync($"{I18n.TitleAddedToYourCollection}: {library.Manga.Title} ", cancellationToken);

            var preferences = dbContext.UserPreferences.FindOne(p => true);
            preferences.SetFilePathTemplate(filePathTemplateFormat);
            dbContext.UserPreferences.Upsert(preferences);

            return Partial("_LibraryCard", library);
        }

        public async Task<IActionResult> OnPostRemoveFromCollectionAsync(CancellationToken cancellationToken)
        {
            ModelState.Remove(nameof(FilePathTemplate));

            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid manga data.");
            }

            var library = dbContext.Libraries.Include(p => p.Manga)
                                             .Include(p => p.CrawlerAgent)
                                             .FindOne(p => p.Manga.Id == MangaId && p.CrawlerAgent.Id == CrawlerAgentId);
            var mangaTitle = library.Manga.Title;

            using var libDbContext = library.GetDbContext();

            var mangaDownload = libDbContext.MangaDownloadRecords.Include(p => p.Library).FindOne(p => p.Library.Id == library.Id);

            if (mangaDownload != null)
            {
               workerService.CancelMangaDownload(mangaDownload);
            }

            library.DropDbContext();

            dbContext.Libraries.Delete(library.Id);

            logger.LogInformation("Drop Database {database}", libDbContext.DatabaseFilePath());

            await notificationService.PushSuccessAsync($"{I18n.YourCollectionNoLongerIncludes}: {mangaTitle}.", cancellationToken);

            return Partial("_LibraryCard", new Library(library.CrawlerAgent, library.Manga, null));
        }
    }
}
