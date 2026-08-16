using Hangfire;
using Hangfire.States;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.AppServices;
/// <summary>
/// DownloadAppService is responsible for managing manga downloads, 
/// including adding and removing items from the collection, 
/// as well as handling chapter download records. It interacts with the database context, 
/// crawler agent repository, worker service, Hangfire repository, and notification service to perform its operations.
/// </summary>
/// <param name="logger">The logger instance for logging information and errors.</param>
/// <param name="specialFolderOptions">The options for special folder configurations.</param>
/// <param name="workerOptions">The options for worker configurations.</param>
/// <param name="dbContext">The database context for accessing the database.</param>
/// <param name="crawlerAgentRepository">The repository for accessing crawler agents.</param>
/// <param name="workerService">The service for managing background worker tasks.</param>
/// <param name="hangfireRepository">The repository for managing Hangfire jobs.</param>
/// <param name="notificationService">The service for sending notifications.</param>
public class DownloadAppService(
    ILogger<DownloadAppService> logger,
    IOptions<SpecialFolderOptions> specialFolderOptions,
    IOptions<WorkerOptions> workerOptions,
    DbContext dbContext,
    ICrawlerAgentRepository crawlerAgentRepository,
    IWorkerService workerService,
    IHangfireRepository hangfireRepository,
    INotificationService notificationService) : IDownloadAppService
{
    /// <inheritdoc />
    public async Task<Library> AddToCollectionAsync(AddItemCollection addItemCollection, CancellationToken cancellationToken)
    {
        using CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(addItemCollection.CrawlerAgentId);

        Manga manga = await crawlerAgentRepository.GetMangaAsync(crawlerAgent.Id, addItemCollection.MangaId, cancellationToken);

        string filePathTemplateFormat = string.IsNullOrWhiteSpace(addItemCollection.FilePathTemplate) ? specialFolderOptions.Value.FilePathFormat : addItemCollection.FilePathTemplate;
        string comicInfoTitleTemplateFormat = string.IsNullOrWhiteSpace(addItemCollection.ComicInfoTitleTemplate) ? specialFolderOptions.Value.ComicInfoTitleFormat : addItemCollection.ComicInfoTitleTemplate;
        string comicInfoSeriesTemplate = string.IsNullOrWhiteSpace(addItemCollection.ComicInfoSeriesTemplate) ? specialFolderOptions.Value.ComicInfoSeriesFormat : addItemCollection.ComicInfoSeriesTemplate;
        TimeSpan? schedule = addItemCollection.DailyExecutionSchedule.HasValue ? addItemCollection.DailyExecutionSchedule : workerOptions.Value.DailyExecutionTime;

        Library library = new(crawlerAgent, manga, filePathTemplateFormat, comicInfoTitleTemplateFormat, comicInfoSeriesTemplate);

        _ = dbContext.Libraries.Insert(library);

        MangaDownloadRecord downloadRecord = new(library, string.Empty);

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        _ = libDbContext.MangaDownloadRecords.Insert(downloadRecord);

        string backgroundJobId = workerService.ScheduleMangaDownload(downloadRecord, schedule);

        downloadRecord.Schedule(backgroundJobId);

        _ = libDbContext.MangaDownloadRecords.Update(downloadRecord);


        if (addItemCollection.MakeThisConfigurationDefault)
        {
            UserPreference preferences = dbContext.UserPreferences.Query().FirstOrDefault();
            preferences.SetFilePathTemplate(filePathTemplateFormat);
            preferences.SetComicInfoTitleTemplate(comicInfoTitleTemplateFormat);
            preferences.SetComicInfoSeriesTemplate(comicInfoSeriesTemplate);
            preferences.SetDailyExecutionTime(addItemCollection.DailyExecutionSchedule);
            _ = dbContext.UserPreferences.Upsert(preferences);
        }

        await notificationService.PushSuccessAsync($"{I18n.TitleAddedToYourCollection}: {library.Manga.Title} ", cancellationToken);

        return library;
    }

    /// <inheritdoc />
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


    /// <inheritdoc />
    public async Task<Library> RemoveFromCollectionAsync(RemoveItemCollection removeItemCollection, CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.Include(p => p.Manga)
                                         .Include(p => p.CrawlerAgent)
                                         .FindOne(p => p.Manga.Id == removeItemCollection.MangaId
                                                    && p.CrawlerAgent.Id == removeItemCollection.CrawlerAgentId);
        string mangaTitle = library.Manga.Title;

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        MangaDownloadRecord mangaDownload = libDbContext.MangaDownloadRecords.Include(p => p.Library).FindOne(p => p.Library.Id == library.Id);

        if (mangaDownload != null)
        {
            workerService.CancelMangaDownload(mangaDownload);
        }

        library.DropDbContext();

        _ = dbContext.Libraries.Delete(library.Id);

        logger.LogInformation("Drop Database {database}", libDbContext.DatabaseFilePath());

        await notificationService.PushSuccessAsync($"{I18n.YourCollectionNoLongerIncludes}: {mangaTitle}.", cancellationToken);

        return library;
    }

    /// <inheritdoc />
    public async Task<ChapterDownloadRecord?> CancelAsync(Guid libraryId, Guid chapterDownloadId, CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return null;
        }

        using LibraryDbContext db = library.GetReadWriteDbContext();

        ChapterDownloadRecord chapterDownloadRecord = db.ChapterDownloadRecords.FindById(chapterDownloadId);

        if (chapterDownloadRecord == null || !chapterDownloadRecord.IsCancellable())
        {
            return null;
        }

        _ = BackgroundJob.Delete(chapterDownloadRecord.BackgroundJobId);

        chapterDownloadRecord.Cancelled("Cancelled by the user.");

        logger.LogInformation("Cancelled by the user.");

        _ = db.ChapterDownloadRecords.Update(chapterDownloadRecord);

        await notificationService.PushSuccessAsync($"{I18n.DownloadChapterHasBeenCancelled}: {library.GetCbzFileName(chapterDownloadRecord.Chapter)}", cancellationToken);

        return chapterDownloadRecord;
    }

    /// <inheritdoc />
    public async Task<ChapterDownloadRecord?> RescheduleAsync(Guid libraryId, Guid chapterDownloadId, CancellationToken cancellationToken)
    {

        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return null;
        }

        using LibraryDbContext db = library.GetReadWriteDbContext();

        ChapterDownloadRecord chapterDownloadRecord = db.ChapterDownloadRecords.FindById(chapterDownloadId);

        if (chapterDownloadRecord == null || !(chapterDownloadRecord.IsCompleted() || chapterDownloadRecord.IsCancelled()))
        {
            return null;
        }

        chapterDownloadRecord.DeleteDownloadedFileIfExists(library);

        EnqueuedState queueState = hangfireRepository.GetLeastLoadedDownloadChapterQueue();

        string jobId = BackgroundJob.Enqueue<IChapterDownloaderJob>(queueState.Queue, worker => worker.DispatchAsync(queueState.Queue,
                                                                                    chapterDownloadRecord.CrawlerAgent.Id,
                                                                                    chapterDownloadRecord.MangaDownload.Library.Id,
                                                                                    chapterDownloadRecord.MangaDownload.Id,
                                                                                    chapterDownloadRecord.Id,
                                                                                    chapterDownloadRecord.MangaDownload.Library.GetCbzFileName(chapterDownloadRecord.Chapter),
                                                                                    null!, CancellationToken.None));

        chapterDownloadRecord.Scheduled(jobId);

        _ = db.ChapterDownloadRecords.Update(chapterDownloadRecord);

        await notificationService.PushSuccessAsync($"{I18n.DownloadChapterSchedule}: {chapterDownloadRecord.MangaDownload.Library.GetCbzFileName(chapterDownloadRecord.Chapter)}", cancellationToken);

        return chapterDownloadRecord;

    }
}
