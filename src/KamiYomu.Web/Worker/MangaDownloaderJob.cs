using Hangfire;
using Hangfire.Server;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Options;

using System.Globalization;

namespace KamiYomu.Web.Worker;

public class MangaDownloaderJob(
    ILogger<MangaDownloaderJob> logger,
    IOptions<WorkerOptions> workerOptions,
    DbContext dbContext,
    ICrawlerAgentRepository agentCrawlerRepository,
    IHangfireRepository hangfireRepository,
    INotificationService notificationService) : IMangaDownloaderJob
{
    private readonly WorkerOptions _workerOptions = workerOptions.Value;

    public async Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, Guid mangaDownloadId, string title, PerformContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Dispatch \"{title}\".", title);

        UserPreference? userPreference = dbContext.UserPreferences.FindOne(p => true);
        CultureInfo culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        Library? library = dbContext.Libraries.FindById(libraryId);
        if (library is null)
        {
            logger.LogWarning("Dispatch \"{title}\" could not proceed — the associated library record no longer exists.", title);
            return;
        }
        using LibraryDbContext libDbContext = library.GetDbContext();
        MangaDownloadRecord mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Id == mangaDownloadId);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!mangaDownload.ShouldRun())
            {
                logger.LogWarning(
                "Dispatch \"{Title}\" failed - job cannot run with status {Status}.",
                title,
                mangaDownload.DownloadStatus);
                return;
            }

            CrawlerAgent crawlerAgent = mangaDownload.Library.CrawlerAgent;
            string mangaId = mangaDownload.Library.Manga.Id;

            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Dispatch '{title}' process started: assigned to Agent Crawler '{AgentCrawler}'", title, crawlerAgent.DisplayName);

            mangaDownload.Processing();

            _ = libDbContext.MangaDownloadRecords.Update(mangaDownload);

            int offset = 0;
            const int limit = 30;
            int? total = null;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                PagedResult<Chapter> page = await agentCrawlerRepository.GetMangaChaptersAsync(crawlerAgent.Id, mangaId, new PaginationOptions(offset, limit), cancellationToken);

                total = page.PaginationOptions.Total;

                foreach (Chapter chapter in page.Data)
                {
                    ChapterDownloadRecord record = libDbContext.ChapterDownloadRecords.FindOne(p => p.CrawlerAgent.Id == crawlerAgent.Id
                                                                                  && p.Chapter.Id == chapter.Id
                                                                                  && p.MangaDownload.Id == mangaDownloadId)
                                                                                  ?? new ChapterDownloadRecord(crawlerAgent, mangaDownload, chapter);

                    if (record.IsDownloadedFileExists(library))
                    {
                        record.Complete();
                        _ = libDbContext.ChapterDownloadRecords.Upsert(record);
                        logger.LogInformation("Dispatch '{title}': File {file} has been found, download chapter marked as completed.", title, library.GetCbzFileName(record.Chapter));
                        continue;
                    }

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

            mangaDownload.Complete();

            _ = libDbContext.MangaDownloadRecords.Update(mangaDownload);

            logger.LogInformation(
            "Dispatch \"{Title}\" (MangaId: {MangaId}) — total chapters found: {Total}",
             title,
             mangaDownload.Library.Manga.Id,
             total);

            if (userPreference.FamilySafeMode && mangaDownload.Library.Manga.IsFamilySafe || !userPreference.FamilySafeMode)
            {
                await notificationService.PushSuccessAsync($"{mangaDownload.Library.Manga.Title}: {I18n.SearchForChaptersCompleted}.", cancellationToken);
            }

        }
        catch (Exception ex) when (!context.CancellationToken.ShutdownToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Dispatch '{title}': completed with error {Message}.", title, ex.Message);
            mangaDownload.Pending(ex.Message);
            _ = libDbContext.MangaDownloadRecords.Update(mangaDownload);
            throw;
        }
        finally
        {
            context.SetJobParameter(nameof(Library.CrawlerAgent), library.CrawlerAgent.DisplayName);
            context.SetJobParameter(nameof(Library.Manga), library.Manga.Title);
            context.SetJobParameter(nameof(Library.Manga.WebSiteUrl), library.Manga.WebSiteUrl);
        }

        logger.LogInformation("Dispatch \"{title}\" completed.", title);
    }
}
