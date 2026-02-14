using Hangfire;
using Hangfire.States;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Models;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.AppServices;

public class DownloadAppService(
    ILogger<DownloadAppService> logger,
    IOptions<SpecialFolderOptions> specialFolderOptions,
    DbContext dbContext,
    ICrawlerAgentRepository crawlerAgentRepository,
    IWorkerService workerService,
    IHangfireRepository hangfireRepository,
    INotificationService notificationService) : IDownloadAppService
{
    public async Task<Library> AddToCollectionAsync(AddItemCollection addItemCollection, CancellationToken cancellationToken)
    {
        using CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(addItemCollection.CrawlerAgentId);

        Manga manga = await crawlerAgentRepository.GetMangaAsync(crawlerAgent.Id, addItemCollection.MangaId, cancellationToken);

        string filePathTemplateFormat = string.IsNullOrWhiteSpace(addItemCollection.FilePathTemplate) ? specialFolderOptions.Value.FilePathFormat : addItemCollection.FilePathTemplate;
        string comicInfoTitleTemplateFormat = string.IsNullOrWhiteSpace(addItemCollection.ComicInfoTitleTemplate) ? specialFolderOptions.Value.ComicInfoTitleFormat : addItemCollection.ComicInfoTitleTemplate;
        string comicInfoSeriesTemplate = string.IsNullOrWhiteSpace(addItemCollection.ComicInfoSeriesTemplate) ? specialFolderOptions.Value.ComicInfoSeriesFormat : addItemCollection.ComicInfoSeriesTemplate;

        Library library = new(crawlerAgent, manga, filePathTemplateFormat, comicInfoTitleTemplateFormat, comicInfoSeriesTemplate);

        _ = dbContext.Libraries.Insert(library);

        MangaDownloadRecord downloadRecord = new(library, string.Empty);

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        _ = libDbContext.MangaDownloadRecords.Insert(downloadRecord);

        string backgroundJobId = workerService.ScheduleMangaDownload(downloadRecord);

        downloadRecord.Schedule(backgroundJobId);

        _ = libDbContext.MangaDownloadRecords.Update(downloadRecord);


        if (addItemCollection.MakeThisConfigurationDefault)
        {
            UserPreference preferences = dbContext.UserPreferences.FindOne(p => true);
            preferences.SetFilePathTemplate(filePathTemplateFormat);
            preferences.SetComicInfoTitleTemplate(comicInfoTitleTemplateFormat);
            preferences.SetComicInfoSeriesTemplate(comicInfoSeriesTemplate);
            _ = dbContext.UserPreferences.Upsert(preferences);
        }

        await notificationService.PushSuccessAsync($"{I18n.TitleAddedToYourCollection}: {library.Manga.Title} ", cancellationToken);

        return library;
    }

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

    public async Task<ChapterDownloadRecord?> CancelAsync(Guid libraryId, Guid chapterDownloadId, CancellationToken cancellationToken)
    {
        Library library = dbContext.Libraries.FindById(libraryId);
        if (library == null)
        {
            return null;
        }

        using LibraryDbContext db = library.GetReadWriteDbContext();

        ChapterDownloadRecord chapterDownloadRecord = db.ChapterDownloadRecords.FindById(chapterDownloadId);

        if (chapterDownloadRecord == null || !chapterDownloadRecord.IsInProgress())
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
