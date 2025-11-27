using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Download
{
    public class IndexModel(
        ILogger<IndexModel> logger,
        DbContext dbContext,
        ICrawlerAgentRepository agentCrawlerRepository,
        IWorkerService workerService,
        INotificationService notificationService) : PageModel
    {
        public IEnumerable<CrawlerAgent> CrawlerAgents { get; set; } = [];

        [BindProperty]
        public string MangaId { get; set; }

        [BindProperty]
        public Guid AgentId { get; set; }

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
            using var agentCrawler = dbContext.CrawlerAgents.FindById(AgentId);

            var manga = await agentCrawlerRepository.GetMangaAsync(agentCrawler, MangaId, cancellationToken);

            var library = new Library(agentCrawler, manga);

            dbContext.Libraries.Insert(library);

            var downloadRecord = new MangaDownloadRecord(library, string.Empty);

            using var libDbContext = library.GetDbContext();

            libDbContext.MangaDownloadRecords.Insert(downloadRecord);

            string backgroundJobId = workerService.ScheduleMangaDownload(downloadRecord);

            downloadRecord.Schedule(backgroundJobId);

            libDbContext.MangaDownloadRecords.Update(downloadRecord);

            await notificationService.PushSuccessAsync($"{I18n.TitleAddedToYourCollection}: {library.Manga.Title} ", cancellationToken);

            return Partial("_LibraryCard", library);
        }



        public async Task<IActionResult> OnPostRemoveFromCollectionAsync(CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid manga data.");
            }

            var library = dbContext.Libraries.Include(p => p.Manga)
                                             .Include(p => p.AgentCrawler)
                                             .FindOne(p => p.Manga.Id == MangaId && p.AgentCrawler.Id == AgentId);
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

            return Partial("_LibraryCard", new Library(library.AgentCrawler, library.Manga));
        }
    }
}
