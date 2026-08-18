

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Infrastructure.AppServices;

public class CrawlerAgentAppService(ILogger<CrawlerAgentAppService> logger,
                                    DbContext dbContext,
                                    ICrawlerAgentRepository crawlerAgentRepository,
                                    IWorkerService workerService) : ICrawlerAgentAppService
{
    /// <inheritdoc/>
    public async Task<Library> RefreshCollectionAsync(Library library, CancellationToken cancellationToken)
    {
        using CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(library.CrawlerAgent.Id);

        Manga manga = await crawlerAgentRepository.GetMangaAsync(crawlerAgent.Id, library.Manga.Id, cancellationToken);

        MangaDownloadRecord downloadRecord = new(library, string.Empty);

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        _ = libDbContext.MangaDownloadRecords.Insert(downloadRecord);

        string backgroundJobId = workerService.ScheduleMangaDownload(downloadRecord, null);

        downloadRecord.Schedule(backgroundJobId);

        _ = libDbContext.MangaDownloadRecords.Update(downloadRecord);

        return library;
    }

    /// <inheritdoc/>
    public async Task<Library> UpgradeCrawlerAgentAsync(Guid libraryId, Guid crawlerAgentId, CancellationToken cancellationToken)
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
                library = await RefreshCollectionAsync(library, cancellationToken);
            }

            _ = dbContext.Libraries.Update(library);

        }

        return library;
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
