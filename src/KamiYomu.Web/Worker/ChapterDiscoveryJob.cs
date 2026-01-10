using System.Globalization;

using Hangfire;
using Hangfire.Server;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Worker;

public class ChapterDiscoveryJob(
    ILogger<ChapterDiscoveryJob> logger,
    IOptions<WorkerOptions> workerOptions,
    ICrawlerAgentRepository agentCrawlerRepository,
    IHangfireRepository hangfireRepository,
    DbContext dbContext) : IChapterDiscoveryJob
{
    private readonly WorkerOptions _workerOptions = workerOptions.Value;

    public async Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, PerformContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Dispatch \"{title}\".", nameof(ChapterDiscoveryJob));

        UserPreference userPreference = dbContext.UserPreferences.FindOne(p => true);
        CultureInfo culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        if (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Dispatch cancelled {JobName}", nameof(ChapterDiscoveryJob));
            return;
        }

        Library library = dbContext.Libraries.FindById(libraryId);

        if (library == null)
        {
            logger.LogWarning("{Dispatch} for '{libraryId}' could not proceed — the associated library record no longer exists.", nameof(DispatchAsync), libraryId);
            return;
        }

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();
        MangaDownloadRecord mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == libraryId);
        string[] files = Directory.GetFiles(library.GetMangaDirectory(), "*.cbz", SearchOption.AllDirectories);

        CrawlerAgent crawlerAgent = mangaDownload.Library.CrawlerAgent;
        string mangaId = mangaDownload.Library.Manga!.Id;

        int offset = 0;
        const int limit = 100;
        int? total = null;

        logger.LogInformation("Starting '{jobname}' for manga: '{MangaId}'", nameof(ChapterDiscoveryJob), mangaId);

        do
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Dispatch cancelled during chapter fetch for manga: '{MangaId}'", mangaId);
                mangaDownload.Cancelled($"Cancelled during the running job: {mangaId}");
                _ = libDbContext.MangaDownloadRecords.Update(mangaDownload);
                return;
            }

            PagedResult<Chapter> page = await agentCrawlerRepository.GetMangaChaptersAsync(
                crawlerAgent.Id, mangaId, new PaginationOptions(offset, limit), cancellationToken);

            total = page.PaginationOptions.Total;

            foreach (Chapter chapter in page.Data)
            {
                if (File.Exists(library.GetCbzFilePath(chapter)))
                {
                    continue;
                }

                ChapterDownloadRecord record = libDbContext.ChapterDownloadRecords
                                         .FindOne(p => p.Chapter!.Id == chapter.Id
                                                    && p.CrawlerAgent!.Id == crawlerAgent.Id)
                                         ?? new ChapterDownloadRecord(crawlerAgent, mangaDownload, chapter);

                if (record.IsInProgress() || (record.IsCompleted() && record.LastUpdatedStatusTotalDays() < 1))
                {
                    continue;
                }

                record.ToBeRescheduled();

                _ = libDbContext.ChapterDownloadRecords.Upsert(record);
                Hangfire.States.EnqueuedState queueState = hangfireRepository.GetLeastLoadedDownloadChapterQueue();
                string backgroundJobId = BackgroundJob.Enqueue<IChapterDownloaderJob>(queueState.Queue, p => p.DispatchAsync(queueState.Queue, library.CrawlerAgent.Id, library.Id, mangaDownload.Id, record.Id, library.GetCbzFileName(chapter), null!, CancellationToken.None));

                record.Scheduled(backgroundJobId);
                _ = libDbContext.ChapterDownloadRecords.Update(record);
                await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
            }

            offset += limit;
            await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
        } while (offset < total);


        context.SetJobParameter(nameof(library.CrawlerAgent), library.CrawlerAgent.DisplayName);
        context.SetJobParameter(nameof(library.Manga), library.Manga.Title);
        context.SetJobParameter(nameof(library.Manga.WebSiteUrl), library.Manga.WebSiteUrl);
        logger.LogInformation("Dispatch \"{title}\" completed.", nameof(ChapterDiscoveryJob));
    }
}
