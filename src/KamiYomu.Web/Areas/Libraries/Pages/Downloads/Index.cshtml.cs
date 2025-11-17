using Hangfire;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Libraries.Pages.Download
{
    public class IndexModel(
        ILogger<IndexModel> logger,
        IOptionsSnapshot<WorkerOptions> workerOptions,
        DbContext dbContext,
        IAgentCrawlerRepository agentCrawlerRepository,
        IBackgroundJobClient jobClient,
        IHangfireRepository hangfireRepository,
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
            var agentCrawler = dbContext.CrawlerAgents.FindById(AgentId);

            var manga = await agentCrawlerRepository.GetMangaAsync(agentCrawler, MangaId, cancellationToken);

            var library = new Library(agentCrawler, manga);

            dbContext.Libraries.Insert(library);

            var downloadRecord = new MangaDownloadRecord(library, string.Empty);

            using var libDbContext = library.GetDbContext();

            libDbContext.MangaDownloadRecords.Insert(downloadRecord);

            var backgroundJobId = jobClient.Create<IMangaDownloaderJob>(
                p => p.DispatchAsync(library.AgentCrawler.Id, library.Id, downloadRecord.Id, manga.Title, null!, CancellationToken.None),
                hangfireRepository.GetLeastLoadedMangaDownloadSchedulerQueue()
            );

            RecurringJob.AddOrUpdate<IChapterDiscoveryJob>(
            library.GetDiscovertyJobId(),
            Defaults.Worker.DiscoveryNewChapterQueues,
            (job) => job.DispatchAsync(agentCrawler.Id, library.Id, null!, cancellationToken),
            Cron.Daily());

            downloadRecord.Schedule(backgroundJobId);

            libDbContext.MangaDownloadRecords.Update(downloadRecord);

            await notificationService.PushInfoAsync($"Title {library.Manga.Title} was added to your collection.", cancellationToken);

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

            using var libDbContext = library.GetDbContext();

            var mangaDownload = libDbContext.MangaDownloadRecords.Include(p => p.Library).FindOne(p => p.Library.Id == library.Id);

            if (mangaDownload != null)
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
            var mangaTitle = library.Manga.Title;
            RecurringJob.RemoveIfExists(library.GetDiscovertyJobId());

            library.DropDbContext();

            dbContext.Libraries.Delete(library.Id);

            logger.LogInformation("Drop Database {database}", libDbContext.DatabaseFilePath());

            await notificationService.PushWarningAsync($"Title {mangaTitle} was removed from your collection.", cancellationToken);

            return Partial("_LibraryCard", new Library(library.AgentCrawler, library.Manga));
        }
    }
}
