using System.Globalization;

using Hangfire;
using Hangfire.Server;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Worker;

/// <summary>
/// Job for discovering and scheduling chapter downloads for manga in a library.
/// </summary>
/// <param name="logger"></param>
/// <param name="workerOptions"></param>
/// <param name="agentCrawlerRepository"></param>
/// <param name="hangfireRepository"></param>
/// <param name="dbContext"></param>
public class ChapterDiscoveryJob(
    ILogger<ChapterDiscoveryJob> logger,
    IOptions<WorkerOptions> workerOptions,
    ICrawlerAgentRepository agentCrawlerRepository,
    IHangfireRepository hangfireRepository,
    DbContext dbContext) : IChapterDiscoveryJob
{
    private readonly WorkerOptions _workerOptions = workerOptions.Value;

    /// <inheritdoc/>
    public async Task DispatchAsync(string queue, Guid crawlerAgentId, Guid libraryId, PerformContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Dispatch \"{title}\".", nameof(ChapterDiscoveryJob));
        context.SetJobParameter(Defaults.Worker.CrawlerAgentId, crawlerAgentId);
        context.SetJobParameter(Defaults.Worker.LibraryId, libraryId);

        SetupCultureInfo();

        if (!CanProceedWithDispatch(cancellationToken))
        {
            return;
        }

        Library library = dbContext.Libraries.FindById(libraryId);

        if (!ValidateLibrary(library, libraryId))
        {
            return;
        }

        await UpdateMangaRecordAsync(library, cancellationToken);

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        MangaDownloadRecord mangaDownload = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == libraryId);

        mangaDownload.UpdateLibraryInformation(library);

        await DiscoverAndScheduleChaptersAsync(libDbContext, mangaDownload, library, cancellationToken);

        CompleteDownloadRecord(libDbContext, mangaDownload);

        context.SetJobParameter(nameof(library.CrawlerAgent), library.CrawlerAgent.DisplayName);
        context.SetJobParameter(nameof(library.Manga), library.Manga.Title);
        context.SetJobParameter(nameof(library.Manga.WebSiteUrl), library.Manga.WebSiteUrl);

        logger.LogInformation("Dispatch \"{title}\" completed.", nameof(ChapterDiscoveryJob));
    }

    private void SetupCultureInfo()
    {
        UserPreference userPreference = dbContext.UserPreferences.FindOne(p => true);
        CultureInfo culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    private bool CanProceedWithDispatch(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Dispatch cancelled {JobName}", nameof(ChapterDiscoveryJob));
            return false;
        }

        return true;
    }

    private bool ValidateLibrary(Library library, Guid libraryId)
    {
        if (library == null)
        {
            logger.LogWarning("{Dispatch} for '{libraryId}' could not proceed — the associated library record no longer exists.", nameof(DispatchAsync), libraryId);
            return false;
        }

        return true;
    }

    private async Task DiscoverAndScheduleChaptersAsync(LibraryDbContext libDbContext, MangaDownloadRecord mangaDownload, Library library, CancellationToken cancellationToken)
    {
        CrawlerAgent crawlerAgent = mangaDownload.Library.CrawlerAgent;
        string? mangaId = mangaDownload.Library.Manga!.Id;

        int offset = 0;
        const int limit = 30;
        string? continuationToken = null;
        bool fetchMoreChapters = true;

        logger.LogInformation("Starting '{jobname}' for manga: '{MangaId}'", nameof(ChapterDiscoveryJob), mangaId);

        do
        {
            if (cancellationToken.IsCancellationRequested)
            {
                HandleCancellationDuringFetch(libDbContext, mangaDownload, mangaId);
                return;
            }

            PaginationOptions paginationOptions = BuildPaginationOptions(continuationToken, offset, limit);

            PagedResult<Chapter> page = await agentCrawlerRepository.GetMangaChaptersAsync(
                crawlerAgent.Id, mangaId, paginationOptions, cancellationToken);

            await ProcessChaptersAsync(libDbContext, page.Data, mangaDownload, library, crawlerAgent, cancellationToken);

            continuationToken = page.PaginationOptions.ContinuationToken;
            offset += limit;
            fetchMoreChapters = DetermineFetchMoreChapters(page, offset);

            await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
        } while (fetchMoreChapters);
    }

    private PaginationOptions BuildPaginationOptions(string? continuationToken, int offset, int limit)
    {
        return !string.IsNullOrWhiteSpace(continuationToken)
            ? new PaginationOptions(continuationToken)
            : new PaginationOptions(offset, limit);
    }

    private async Task ProcessChaptersAsync(LibraryDbContext libDbContext, IEnumerable<Chapter> chapters, MangaDownloadRecord mangaDownload, Library library, CrawlerAgent crawlerAgent, CancellationToken cancellationToken)
    {
        foreach (Chapter chapter in chapters)
        {
            if (File.Exists(library.GetCbzFilePath(chapter)))
            {
                continue;
            }

            ChapterDownloadRecord record = GetOrCreateChapterRecord(libDbContext, chapter, crawlerAgent, mangaDownload);

            if (ShouldSkipChapterRecord(record))
            {
                continue;
            }

            await ScheduleChapterDownloadAsync(libDbContext, record, library, mangaDownload, chapter, cancellationToken);
        }
    }

    private ChapterDownloadRecord GetOrCreateChapterRecord(LibraryDbContext libDbContext, Chapter chapter, CrawlerAgent crawlerAgent, MangaDownloadRecord mangaDownload)
    {
        ChapterDownloadRecord record = libDbContext.ChapterDownloadRecords
            .FindOne(p => p.Chapter!.Id == chapter.Id
                       && p.CrawlerAgent!.Id == crawlerAgent.Id)
            ?? new ChapterDownloadRecord(crawlerAgent, mangaDownload, chapter);

        return record;
    }

    private bool ShouldSkipChapterRecord(ChapterDownloadRecord record)
    {
        return record.IsInProgress() || (record.IsCompleted() && record.LastUpdatedStatusTotalDays() < 1);
    }

    private async Task ScheduleChapterDownloadAsync(LibraryDbContext libDbContext, ChapterDownloadRecord record, Library library, MangaDownloadRecord mangaDownload, Chapter chapter, CancellationToken cancellationToken)
    {
        record.ToBeRescheduled();
        _ = libDbContext.ChapterDownloadRecords.Upsert(record);

        Hangfire.States.EnqueuedState queueState = hangfireRepository.GetLeastLoadedDownloadChapterQueue();
        string? backgroundJobId = BackgroundJob.Enqueue<IChapterDownloaderJob>(
            queueState.Queue,
            p => p.DispatchAsync(
                queueState.Queue,
                library.CrawlerAgent.Id,
                library.Id,
                mangaDownload.Id,
                record.Id,
                library.GetCbzFileName(chapter),
                null!,
                CancellationToken.None));

        record.Scheduled(backgroundJobId);
        _ = libDbContext.ChapterDownloadRecords.Update(record);

        await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
    }

    private bool DetermineFetchMoreChapters(PagedResult<Chapter> page, int offset)
    {
        return !string.IsNullOrWhiteSpace(page.PaginationOptions.ContinuationToken)
            ? page.Data.Count() > 0
            : offset < page.PaginationOptions.Total;
    }

    private void HandleCancellationDuringFetch(LibraryDbContext libDbContext, MangaDownloadRecord mangaDownload, string mangaId)
    {
        logger.LogWarning("Dispatch cancelled during chapter fetch for manga: '{MangaId}'", mangaId);
        mangaDownload.Cancelled(string.Format(I18n.CancelledDuringTheRunningJob, mangaId));
        _ = libDbContext.MangaDownloadRecords.Update(mangaDownload);
    }

    private void CompleteDownloadRecord(LibraryDbContext libDbContext, MangaDownloadRecord mangaDownload)
    {
        if (mangaDownload.DownloadStatus != DownloadStatus.Completed)
        {
            mangaDownload.Complete();
        }

        _ = libDbContext.MangaDownloadRecords.Update(mangaDownload);
    }

    private async Task UpdateMangaRecordAsync(Library library, CancellationToken cancellationToken)
    {
        if (library == null)
        {
            return;
        }

        using ICrawlerAgent? crawlerAgent = library.CrawlerAgent.GetCrawlerInstance();

        if (crawlerAgent == null)
        {
            return;
        }

        Manga? updatedManga = await crawlerAgent.GetByIdAsync(library.Manga.Id, cancellationToken);

        if (updatedManga != null)
        {
            library.UpdateMangaInformation(updatedManga);
            _ = dbContext.Libraries.Update(library);
        }
    }
}
