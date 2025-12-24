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

        var userPreference = dbContext.UserPreferences.FindOne(p => true);
        var culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var library = dbContext.Libraries.FindById(libraryId);
        if(library is null)
        {
            logger.LogWarning("Dispatch \"{title}\" could not proceed — the associated library record no longer exists.", title);
            return;
        }
        using var libDbContext = library.GetDbContext();
        var mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Id == mangaDownloadId);
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

            var crawlerAgent = mangaDownload.Library.CrawlerAgent;
            var mangaId = mangaDownload.Library.Manga.Id;
            
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Dispatch '{title}' process started: assigned to Agent Crawler '{AgentCrawler}'", title, crawlerAgent.DisplayName);

            mangaDownload.Processing();

            libDbContext.MangaDownloadRecords.Update(mangaDownload);

            int offset = 0;
            const int limit = 30;
            int? total = null;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = await agentCrawlerRepository.GetMangaChaptersAsync(crawlerAgent.Id, mangaId, new PaginationOptions(offset, limit), cancellationToken);

                total = page.PaginationOptions.Total;

                foreach (var chapter in page.Data)
                {
                    var record = libDbContext.ChapterDownloadRecords.FindOne(p => p.CrawlerAgent.Id == crawlerAgent.Id
                                                                                  && p.Chapter.Id == chapter.Id
                                                                                  && p.MangaDownload.Id == mangaDownloadId) 
                                                                                  ?? new ChapterDownloadRecord(crawlerAgent, mangaDownload, chapter);

                    if(record.IsDownloadedFileExists(library))
                    {
                        record.Complete();
                        libDbContext.ChapterDownloadRecords.Upsert(record);
                        logger.LogInformation("Dispatch '{title}': File {file} has been found, download chapter marked as completed.", title, library.GetCbzFileName(record.Chapter));
                        continue;
                    }

                    libDbContext.ChapterDownloadRecords.Upsert(record);
                    var queueState = hangfireRepository.GetLeastLoadedDownloadChapterQueue();
                    var backgroundJobId = BackgroundJob.Enqueue<IChapterDownloaderJob>(queueState.Queue, p => p.DispatchAsync(queueState.Queue, library.CrawlerAgent.Id, library.Id, mangaDownload.Id, record.Id, library.GetCbzFileName(chapter), null!, CancellationToken.None) );

                    record.Scheduled(backgroundJobId);
                    libDbContext.ChapterDownloadRecords.Update(record);
                    await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
                }

                offset += limit;
                await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
            } while (offset < total);

            mangaDownload.Complete();

            libDbContext.MangaDownloadRecords.Update(mangaDownload);

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
            libDbContext.MangaDownloadRecords.Update(mangaDownload);
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
