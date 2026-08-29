using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Infrastructure.AppServices;
/// <summary>
/// 
/// </summary>
/// <param name="logger"></param>
/// <param name="dbContext"></param>
/// <param name="crawlerAgentRepository"></param>
/// <param name="workerService"></param>
public class CrawlerAgentAppService(ILogger<CrawlerAgentAppService> logger,
                                    DbContext dbContext,
                                    ICrawlerAgentRepository crawlerAgentRepository,
                                    IWorkerService workerService,
                                    INotificationService notificationService) : ICrawlerAgentAppService
{

    /// <inheritdoc/>
    public async Task<IEnumerable<Library>> ConsolidateCollectionByCrawlerAgentAsync(CrawlerAgent crawlerAgent, CancellationToken cancellationToken)
    {
        List<Library> libraries = dbContext.Libraries.Query().Where(p => p.CrawlerAgent.Id == crawlerAgent.Id).ToList();
        for (int i = 0; i < libraries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _ = notificationService.PushInfoAsync(string.Format("{0} — {1}/{2}", libraries[i].Manga.Title, i + 1, libraries.Count), cancellationToken);

            libraries[i] = await UpgradeCrawlerAgentAsync(libraries[i].Id, crawlerAgent.Id, cancellationToken);

            _ = notificationService.PushSuccessAsync(string.Format("{0} — {1}/{2}", libraries[i].Manga.Title, i + 1, libraries.Count), cancellationToken);
        }

        return libraries;
    }


    /// <inheritdoc/>
    public async Task<IEnumerable<Library>> ConsolidateCollectionByAssemblyNameAsync(CrawlerAgent crawlerAgent, CancellationToken cancellationToken)
    {
        List<Library> libraries = dbContext.Libraries.Query().Where(p => p.CrawlerAgent.AssemblyName == crawlerAgent.AssemblyName).ToList();
        for (int i = 0; i < libraries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _ = notificationService.PushInfoAsync(string.Format("{0} — {1}/{2}", libraries[i].Manga.Title, i + 1, libraries.Count), cancellationToken);

            libraries[i] = await UpgradeCrawlerAgentAsync(libraries[i].Id, crawlerAgent.Id, cancellationToken);

            _ = notificationService.PushSuccessAsync(string.Format("{0} — {1}/{2}", libraries[i].Manga.Title, i + 1, libraries.Count), cancellationToken);
        }

        _ = dbContext.Libraries.Update(libraries);
        return libraries;
    }

    /// <inheritdoc/>
    public async Task<Library> UpgradeCrawlerAgentAsync(Guid libraryId, Guid crawlerAgentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Library library = dbContext.Libraries.FindOne(p => p.Id == libraryId);

        if (crawlerAgentId != Guid.Empty)
        {
            TimeSpan? scheduled = workerService.GetDiscoverySchedule(library);

            CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindOne(p => p.Id == crawlerAgentId);

            library.SetCrawlerAgent(crawlerAgent);

            using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

            MangaDownloadRecord mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == libraryId);

            if (mangaDownload != null)
            {
                mangaDownload.UpdateLibraryInformation(library);

                workerService.CancelMangaDownload(mangaDownload);

                string backgroundJobId = workerService.ScheduleMangaDownload(mangaDownload, scheduled);

                mangaDownload.Schedule(backgroundJobId, I18n.CrawlerAgentHasBeenUpgraded);

                UpdateChapterDownloadRecords(crawlerAgent, libDbContext, mangaDownload);

                _ = libDbContext.MangaDownloadRecords.Update(mangaDownload);
            }
            else
            {
                library = await RefreshCollectionAsync(library, cancellationToken);
            }

            _ = dbContext.Libraries.Update(library);

        }

        return library;
    }

    /// <inheritdoc/>
    public async Task<Library> RefreshCollectionAsync(Library library, CancellationToken cancellationToken)
    {
        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        MangaDownloadRecord mangaDownloadRecord = new(library, string.Empty);

        _ = libDbContext.MangaDownloadRecords.Insert(mangaDownloadRecord);

        string backgroundJobId = workerService.ScheduleMangaDownload(mangaDownloadRecord, null);

        mangaDownloadRecord.Schedule(backgroundJobId);

        _ = libDbContext.MangaDownloadRecords.Update(mangaDownloadRecord);

        return library;
    }

    private void UpdateChapterDownloadRecords(CrawlerAgent crawlerAgent, LibraryDbContext libDbContext, MangaDownloadRecord mangaDownload)
    {

        List<ChapterDownloadRecord> chapterDownloads = libDbContext.ChapterDownloadRecords
                                                                   .Query()
                                                                   .Where(p => p.MangaDownload.Id == mangaDownload.Id)
                                                                   .ToList();

        foreach (ChapterDownloadRecord chapterDownload in chapterDownloads)
        {
            chapterDownload.UpdateMangaDownloadInformation(mangaDownload);
            chapterDownload.UpdateCrawlerAgentInformation(crawlerAgent);
            if (chapterDownload.DownloadStatus is not DownloadStatus.Completed)
            {
                chapterDownload.ToBeRescheduled(I18n.CrawlerAgentHasBeenUpgraded);
                workerService.CancelChapterDownload(chapterDownload);
            }
            _ = libDbContext.ChapterDownloadRecords.Update(chapterDownload);
        }
    }
}
