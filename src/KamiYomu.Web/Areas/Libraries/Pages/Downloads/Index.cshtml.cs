using Hangfire;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Download
{
    public class IndexModel(
        ILogger<IndexModel> logger,
        DbContext dbContext, 
        IAgentCrawlerRepository agentCrawlerRepository, 
        IBackgroundJobClient jobClient,
        IHangfireRepository hangfireRepository) : PageModel
    {
        public IEnumerable<CrawlerAgent> CrawlerAgents { get; set; }

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
            var agentCrawler = dbContext.CrawlerAgents.FindById(AgentId);

            var manga = await agentCrawlerRepository.GetMangaAsync(agentCrawler, MangaId, cancellationToken);

            var library = new Library(agentCrawler, manga);

            dbContext.Libraries.Insert(library);

            var downloadRecord = new MangaDownloadRecord(library, string.Empty);

            using var libDbContext = library.GetDbContext();

            libDbContext.MangaDownloadRecords.Insert(downloadRecord);

            var backgroundJobId = jobClient.Create<IMangaDownloaderJob>(
                p => p.DispatchAsync(library.Id, downloadRecord.Id, manga.Title, null!, CancellationToken.None),
                hangfireRepository.GetLeastLoadedMangaDownloadSchedulerQueue()
            );

            downloadRecord.Schedule(backgroundJobId);

            libDbContext.MangaDownloadRecords.Update(downloadRecord);

            return Partial("_LibraryCard", library);
        }

        public IActionResult OnPostRemoveFromCollection()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("Invalid manga data.");
            }

            var library = dbContext.Libraries.Include(p => p.Manga)
                                             .Include(p => p.AgentCrawler)
                                             .FindOne(p => p.Manga.Id == MangaId && p.AgentCrawler.Id == AgentId);

            using var libDbContext = library.GetDbContext(); 

            var mangaDownload = libDbContext.MangaDownloadRecords.Include(p => p.Library).FindOne(p => p.Library.Id == library.Id);

            if(mangaDownload != null)
            {
                mangaDownload.Cancelled("User remove manga from the library.");

                if (!string.IsNullOrWhiteSpace(mangaDownload.BackgroundJobId))
                {
                    jobClient.Delete(mangaDownload.BackgroundJobId);
                }

                var chapterDownloads = libDbContext.ChapterDownloadRecords.FindAll();
                foreach (var chapterDownload in chapterDownloads)
                {
                    if (!string.IsNullOrWhiteSpace(chapterDownload.BackgroundJobId))
                    {
                        jobClient.Delete(chapterDownload.BackgroundJobId);
                    }
                }

                libDbContext.MangaDownloadRecords.Update(mangaDownload);
            }

            library.DropDbContext();

            dbContext.Libraries.Delete(library.Id);

            logger.LogInformation("Drop Database {database}", libDbContext.DatabaseFilePath());

            return Partial("_LibraryCard", new Entities.Library(library.AgentCrawler, library.Manga));
        }
    }
}
