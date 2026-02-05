using System.Globalization;
using System.IO.Compression;

using Hangfire;
using Hangfire.Server;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Worker;

public class ChapterDownloaderJob(
    ILogger<ChapterDownloaderJob> logger,
    IOptions<WorkerOptions> workerOptions,
    DbContext dbContext,
    CacheContext cacheContext,
    ICrawlerAgentRepository agentCrawlerRepository,
    IHttpClientFactory httpClientFactory,
    IHangfireRepository hangfireRepository,
    INotificationService notificationService,
    IGotifyService gotifyService) : IChapterDownloaderJob, IDisposable
{
    private readonly WorkerOptions _workerOptions = workerOptions.Value;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(Defaults.Worker.HttpClientApp);
    private bool disposedValue;

    public async Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, Guid mangaDownloadId, Guid chapterDownloadId, string title, PerformContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Dispatch \"{title}\".", title);

        UserPreference? userPreference = dbContext.UserPreferences
                                                  .Include(p => p.KavitaSettings)
                                                  .Include(p => p.GotifySettings)
                                                  .Query()
                                                  .FirstOrDefault();

        CultureInfo culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        Library? library = dbContext.Libraries.FindById(libraryId);

        if (library is null)
        {
            logger.LogWarning("Dispatch '{title}' could not proceed — the associated library record no longer exists.", title);
            return;
        }

        using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

        MangaDownloadRecord mangaDownload = libDbContext.MangaDownloadRecords
            .Include(p => p.Library)
            .FindById(mangaDownloadId);

        ChapterDownloadRecord? chapterDownload = libDbContext.ChapterDownloadRecords.FindById(chapterDownloadId);

        try
        {
            if (chapterDownload is null)
            {
                logger.LogWarning("Dispatch '{title}' could not proceed — ChapterDownloadRecord not found: '{ChapterDownloadId}'", title, chapterDownloadId);
                return;
            }

            if (!chapterDownload.ShouldRun())
            {
                logger.LogWarning(
                "Dispatch \"{Title}\" failed - job cannot run with status {Status}.",
                title,
                chapterDownload.DownloadStatus);
                return;
            }

            if (File.Exists(library.GetCbzFilePath(chapterDownload.Chapter)))
            {
                chapterDownload.Complete();
                _ = libDbContext.ChapterDownloadRecords.Update(chapterDownload);
                logger.LogWarning("Dispatch '{title}' could not proceed — '{file}' was found, download chapter marked as completed.", title, library.GetCbzFileName(chapterDownload.Chapter));
                return;
            }

            logger.LogInformation("Dispatch '{title}' process started: Chapter assigned to Agent Crawler '{AgentCrawler}'", title, library.CrawlerAgent.DisplayName);

            chapterDownload.Processing();

            _ = libDbContext.ChapterDownloadRecords.Update(chapterDownload);

            IEnumerable<Page> pages = await agentCrawlerRepository.GetChapterPagesAsync(
                chapterDownload.CrawlerAgent.Id,
                chapterDownload.Chapter,
                cancellationToken);

            string tempMangaFolder = mangaDownload.Library.GetTempDirectory();
            string tempChapterFolder = library.GetTempChapterDirectory(chapterDownload.Chapter);

            int pageCount = pages.Count();

            logger.LogInformation(
            "Dispatch '{Title}' using crawler '{Crawler}' — downloading '{Count}' pages into folder: '{ChapterFolderPath}'",
            title,
            library.CrawlerAgent.DisplayName,
            pageCount,
            tempChapterFolder);

            File.WriteAllText(Path.Join(tempChapterFolder, "ComicInfo.xml"), library.ToComicInfo(chapterDownload.Chapter));

            _ = await SaveCoverAsync(library.Manga, tempChapterFolder, cancellationToken);

            int index = 1;

            foreach (Page? page in pages.OrderBy(p => p.PageNumber))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    string fileName = $"{index:D3}-{Path.GetFileName(page.ImageUrl.AbsolutePath)}";

                    string filePath = Path.Combine(tempChapterFolder, fileName);

                    await SavePageAsync(filePath, page, cancellationToken);

                    logger.LogInformation("Dispatch '{Title}' using crawler '{crawler}': Downloaded page '{Index}'/'{count}' to '{FilePath}'", title, library.CrawlerAgent.DisplayName, index, pageCount, filePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Dispatch '{Title}' using crawler '{crawler}': Failed to download page '{Index}'/'{count}' from '{Url}'", title, library.CrawlerAgent.DisplayName, index, pageCount, page.ImageUrl);
                }
                index++;

                await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
            }

            logger.LogInformation("Dispatch '{Title}' using crawler {crawler} Completed download of chapter {ChapterDownloadId} to {ChapterFolder}", title, library.CrawlerAgent.DisplayName, chapterDownloadId, tempMangaFolder);

            int numberOfFiles = CreateCbzFile(chapterDownload, library);
            const int minFilesThreshold = 2;
            if (numberOfFiles <= minFilesThreshold)
            {
                await notificationService.PushWarningAsync($"{I18n.CbzIsTooSmall}: {library.GetCbzFileName(chapterDownload.Chapter)}", cancellationToken);
                chapterDownload.DeleteDownloadedFileIfExists(library);
                string cbzFilePath = Path.Combine(tempMangaFolder, library.GetCbzFileName(chapterDownload.Chapter!));

                if (File.Exists(cbzFilePath))
                {
                    File.Delete(cbzFilePath);
                }

                throw new FileNotFoundException($"{library.GetCbzFileName(chapterDownload.Chapter)} CBZ file size is too small, indicating a failed download.");
            }

            chapterDownload.Complete();
            _ = libDbContext.ChapterDownloadRecords.Update(chapterDownload);

            if ((userPreference.FamilySafeMode && chapterDownload.MangaDownload.Library.Manga.IsFamilySafe) ||
                !userPreference.FamilySafeMode)
            {
                await notificationService.PushSuccessAsync($"{I18n.ChapterDownloaded}: {Path.GetFileNameWithoutExtension(library.GetCbzFileName(chapterDownload.Chapter))}", cancellationToken);
            }

            if (userPreference?.KavitaSettings?.Enabled == true)
            {
                ScheduleKavitaNotify(libraryId);
            }

            if (userPreference?.GotifySettings?.Enabled == true)
            {
                await PushGotifyNotificationAsync(chapterDownload, cancellationToken);
            }
        }
        catch (Exception ex) when (!context.CancellationToken.ShutdownToken.IsCancellationRequested)
        {
            int attempt = context.GetJobParameter<int>("RetryCount") + 1;
            string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            string logMessage = $"{I18n.Attempt} {attempt}/{_workerOptions.MaxRetryAttempts}: {I18n.DispatchFailedMessage}. {I18n.Error}: {errorMessage}";
            logger.LogError(ex, logMessage);
            chapterDownload.ToBeRescheduled(logMessage);
            _ = libDbContext.ChapterDownloadRecords.Update(chapterDownload);
            throw;
        }
        finally
        {
            context.SetJobParameter(nameof(title), title);
            context.SetJobParameter(nameof(library.CrawlerAgent), library.CrawlerAgent.DisplayName);
            context.SetJobParameter(nameof(library.Manga), library.Manga.Title);
            context.SetJobParameter(nameof(library.Manga.WebSiteUrl), library.Manga.WebSiteUrl);
        }

        logger.LogInformation("Dispatch \"{title}\" completed.", title);
    }



    private async Task SavePageAsync(string filePath, Page page, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(
            page.ImageUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        _ = response.EnsureSuccessStatusCode();

        await using Stream httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream fileStream = File.Create(filePath);
        await httpStream.CopyToAsync(fileStream, cancellationToken);
    }

    private async Task<bool> SaveCoverAsync(Manga manga, string mangaFolder, CancellationToken cancellationToken)
    {
        if (manga.CoverUrl == null)
        {
            return false;
        }
        string coverFileName = "cover" + Path.GetExtension(manga.CoverUrl.AbsolutePath);
        string coverFilePath = Path.Combine(mangaFolder, coverFileName);
        if (File.Exists(coverFilePath))
        {
            return false;
        }
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                manga.CoverUrl,
                HttpCompletionOption.ResponseHeadersRead);
            _ = response.EnsureSuccessStatusCode();
            using Stream httpStream = await response.Content.ReadAsStreamAsync();
            using FileStream fileStream = File.Create(coverFilePath);
            httpStream.CopyTo(fileStream);
            logger.LogInformation("Copied cover image to chapter folder: '{CoverFilePath}'", coverFilePath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to copy cover image to chapter folder: '{CoverFilePath}'", coverFilePath);
            return false;
        }
    }
    private int CreateCbzFile(ChapterDownloadRecord chapterDownload, Library library)
    {
        string cbzFilePath = library.GetCbzFilePath(chapterDownload.Chapter);
        string tempChapterFolder = library.GetTempChapterDirectory(chapterDownload.Chapter);

        if (File.Exists(cbzFilePath))
        {
            File.Delete(cbzFilePath);
        }

        int numberOfFiles = Directory.EnumerateFiles(tempChapterFolder).Count();

        ZipFile.CreateFromDirectory(tempChapterFolder, cbzFilePath);

        try
        {
            Directory.Delete(tempChapterFolder, recursive: true);
            logger.LogInformation("Cleaned up extracted chapter folder: '{ChapterFolder}'", tempChapterFolder);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete chapter folder: '{ChapterFolder}'", tempChapterFolder);
        }

        logger.LogInformation("Created CBZ archive: '{CbzFilePath}'", cbzFilePath);

        return numberOfFiles;
    }

    public void ScheduleKavitaNotify(Guid libraryId)
    {
        string cacheJobId = $"{Defaults.Worker.NotifyKavitaJob}-{libraryId}";
        Hangfire.States.EnqueuedState queueState = hangfireRepository.GetNotifyQueue();

        string? existingJobId = cacheContext.Current.Get<string>(cacheJobId);

        if (!string.IsNullOrEmpty(existingJobId))
        {
            _ = BackgroundJob.Delete(existingJobId);
        }

        string newJobId = BackgroundJob.Schedule<INotifyKavitaJob>(
            queueState.Queue,
            d => d.DispatchAsync(queueState.Queue, null!, CancellationToken.None),
            TimeSpan.FromMinutes(5)
        );

        cacheContext.Current.Add(cacheJobId, newJobId, TimeSpan.FromDays(365));
    }

    private async Task PushGotifyNotificationAsync(ChapterDownloadRecord chapterDownload, CancellationToken cancellationToken)
    {
        try
        {
            await gotifyService.PushChapterDownloadedNotificationAsync(chapterDownload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}


