using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

public class UpgradeCrawlerAgentModel(
    DbContext dbContext,
    INotificationService notificationService,
    IDownloadAppService downloadAppService,
    IWorkerService workerService) : PageModel
{
    public Guid LibraryId { get; set; }
    public required string RefreshElementId { get; set; }
    public IEnumerable<CrawlerAgent> AvailableVersions { get; set; }

    public required Library Library { get; set; }
    public void OnGet(Guid libraryId, string refreshElementId)
    {
        RefreshElementId = refreshElementId;
        LibraryId = libraryId;
        Library = dbContext.Libraries.Include(p => p.Manga).Include(p => p.CrawlerAgent).FindOne(p => p.Id == LibraryId);
        AvailableVersions = dbContext.CrawlerAgents.Query().Where(p => p.AssemblyName == Library.CrawlerAgent.AssemblyName).ToList();
    }

    public async Task<IActionResult> OnPostUpgradeCrawlerAgentAsync(Guid libraryId, Guid crawlerAgentId, CancellationToken cancellationToken)
    {

        Library library = dbContext.Libraries.FindOne(p => p.Id == libraryId);

        if (Guid.Empty != crawlerAgentId)
        {
            TimeSpan? scheduled = workerService.GetDiscovertySchedule(library);

            CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindOne(p => p.Id == crawlerAgentId);

            library.SetCrawlerAgent(crawlerAgent);

            using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

            MangaDownloadRecord mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == libraryId);

            if (mangaDownload != null)
            {
                mangaDownload.UpdateLibraryInformation(library);

                UpdateMangaDownloadRecord(crawlerAgent, libDbContext, mangaDownload, scheduled);
            }
            else
            {
                library = await downloadAppService.RefreshCollectionAsync(library, cancellationToken);
            }

            _ = dbContext.Libraries.Update(library);

            await notificationService.PushSuccessAsync(I18n.CrawlerAgentHasBeenUpgraded, cancellationToken);
        }

        return ViewComponent("LibraryCard", new Dictionary<string, object>
        {
            { "library", library },
            { nameof(cancellationToken), cancellationToken }
        });

    }

    private void UpdateMangaDownloadRecord(CrawlerAgent crawlerAgent, LibraryDbContext libDbContext, MangaDownloadRecord mangaDownload, TimeSpan? scheduled)
    {

        workerService.CancelMangaDownload(mangaDownload);

        List<ChapterDownloadRecord> chapterDownloads = libDbContext.ChapterDownloadRecords.Query().Where(p => p.MangaDownload.Id == mangaDownload.Id).ToList();
        foreach (ChapterDownloadRecord chapterDownload in chapterDownloads)
        {
            chapterDownload.UpdateMangaDownloadInformation(mangaDownload);
            chapterDownload.UpdateCrawlerAgentInformation(crawlerAgent);
            chapterDownload.ToBeRescheduled(I18n.CrawlerAgentHasBeenUpgraded);
        }
        string jobId = workerService.ScheduleMangaDownload(mangaDownload, scheduled);

        mangaDownload.Schedule(jobId, I18n.CrawlerAgentHasBeenUpgraded);

        _ = libDbContext.MangaDownloadRecords.Update(mangaDownload);
        _ = libDbContext.ChapterDownloadRecords.Update(chapterDownloads);

    }
}
