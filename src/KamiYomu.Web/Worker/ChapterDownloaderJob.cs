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
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace KamiYomu.Web.Worker
{
    public class ChapterDownloaderJob(
        ILogger<ChapterDownloaderJob> logger,
        IOptions<WorkerOptions> workerOptions,
        DbContext dbContext,
        ICrawlerAgentRepository agentCrawlerRepository,
        IHttpClientFactory httpClientFactory,
        INotificationService notificationService) : IChapterDownloaderJob, IDisposable
    {
        private readonly WorkerOptions _workerOptions = workerOptions.Value;
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient(Defaults.Worker.HttpClientBackground);
        private bool disposedValue;

        public async Task DispatchAsync(string queue, Guid crawlerId, Guid libraryId, Guid mangaDownloadId, Guid chapterDownloadId, string title, PerformContext context, CancellationToken cancellationToken)
        {
            var userPreference = dbContext.UserPreferences.FindOne(p => true);
            var culture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            var library = dbContext.Libraries.FindById(libraryId);
            if(library == null)
            {
                logger.LogError("Library not found: {LibraryId}", libraryId);
                return;
            }
            using var libDbContext = library.GetDbContext();
            var mangaDownload = libDbContext.MangaDownloadRecords
                .Include(p => p.Library)
                .FindById(mangaDownloadId);
            var chapterDownload = libDbContext.ChapterDownloadRecords.FindById(chapterDownloadId);
            try
            {
                if (chapterDownload is null)
                {
                    logger.LogError("ChapterDownloadRecord not found: {ChapterDownloadId}", chapterDownloadId);
                    return;
                }

                if (library == null)
                {
                    chapterDownload.Cancelled(I18n.LibraryNotFound);
                    libDbContext.ChapterDownloadRecords.Update(chapterDownload);
                    logger.LogWarning("Dispatch \"{title}\" could not proceed — the associated library record no longer exists.", title);
                    return;
                }


                if (File.Exists(chapterDownload.Chapter.GetCbzFilePath()))
                {
                    chapterDownload.Complete();
                    libDbContext.ChapterDownloadRecords.Update(chapterDownload);
                    logger.LogInformation("{file} was found, download chapter marked as completed.", chapterDownload.Chapter.GetCbzFileName());
                    return;
                }

                logger.LogInformation("Dispatch started. Chapter: {chapter} on Job Id {JobId}", title, context.BackgroundJob?.Id);
                chapterDownload.Processing();
                libDbContext.ChapterDownloadRecords.Update(chapterDownload);

                var pages = await agentCrawlerRepository.GetChapterPagesAsync(
                    chapterDownload.CrawlerAgent,
                    chapterDownload.Chapter,
                    cancellationToken);

                var seriesFolder = mangaDownload.Library.Manga!.GetTempDirectory();

                var chapterFolderPath = chapterDownload.Chapter.GetChapterFolderPath(seriesFolder);

                Directory.CreateDirectory(chapterFolderPath);

                var pageCount = pages.Count();

                logger.LogInformation("{crawler}: Downloading {Count} pages to chapter folder: {chapterFolderPath}", library.AgentCrawler.DisplayName, pageCount, chapterFolderPath);

                File.WriteAllText(Path.Join(chapterFolderPath, "ComicInfo.xml"), chapterDownload.Chapter.ToComicInfo());

                int index = 1;

                foreach (var page in pages.OrderBy(p => p.PageNumber))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        logger.LogWarning("{crawler}: Dispatch cancelled during page download. Chapter: {ChapterDownloadId}", library.AgentCrawler.DisplayName, chapterDownloadId);
                        chapterDownload.Cancelled($"Dispatch cancelled during page download. Chapter: {chapterDownloadId}");
                        libDbContext.ChapterDownloadRecords.Update(chapterDownload);
                        return;
                    }

                    var fileName = $"{index:D3}-{Path.GetFileName(page.ImageUrl.AbsolutePath)}";
                    var filePath = Path.Combine(chapterFolderPath, fileName);

                    try
                    {
                        using var response = await _httpClient.GetAsync(
                            page.ImageUrl,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken
                        );

                        response.EnsureSuccessStatusCode();

                        await using (var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                        await using (var fileStream = File.Create(filePath))
                        {
                            await httpStream.CopyToAsync(fileStream, cancellationToken);
                        }

                        logger.LogInformation("{crawler}: Downloaded page {Index}/{count} to {FilePath}", library.AgentCrawler.DisplayName, index, pageCount, filePath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "{crawler}: Failed to download page {Index}/{count} from {Url}", library.AgentCrawler.DisplayName, index, pageCount, page.ImageUrl);
                    }

                    index++;

                    await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
                }

                logger.LogInformation("{crawler}: Completed download of chapter {ChapterDownloadId} to {ChapterFolder}", library.AgentCrawler.DisplayName, chapterDownloadId, chapterFolderPath);

                var bytes = CreateCbzFile(chapterDownload, chapterFolderPath, seriesFolder);

                if(bytes < 600)
                {
                    await notificationService.PushWarningAsync($"{I18n.CbzIsTooSmall}: {chapterDownload.Chapter.GetCbzFileName()}", cancellationToken);
                    chapterDownload.DeleteDownloadedFileIfExists();
                    var cbzFilePath = Path.Combine(seriesFolder, chapterDownload.Chapter!.GetCbzFileName());
                    if (File.Exists(cbzFilePath))
                    {
                        File.Delete(cbzFilePath);
                    }

                    
                    throw new Exception($"{chapterDownload.Chapter.GetCbzFileName()} CBZ file size is too small, indicating a failed download.");
                }

                MoveTempCbzFilesToCollection(mangaDownload.Library.Manga);
                chapterDownload.Complete();
                libDbContext.ChapterDownloadRecords.Update(chapterDownload);

                if (userPreference.FamilySafeMode && chapterDownload.MangaDownload.Library.Manga.IsFamilySafe ||
                    !userPreference.FamilySafeMode)
                {
                    await notificationService.PushSuccessAsync($"{I18n.ChapterDownloaded}: {Path.GetFileNameWithoutExtension(chapterDownload.Chapter.GetCbzFileName())}", cancellationToken);
                }
            }
            catch (Exception ex)
            {
                var attempt = context.GetJobParameter<int>("RetryCount") + 1;
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                var logMessage = $"{I18n.Attempt} {attempt}/{_workerOptions.MaxRetryAttempts}: {I18n.DispatchFailedMessage}. {I18n.Error}: {errorMessage}";
                logger.LogError(ex, logMessage);
                chapterDownload.ToBeRescheduled(logMessage);
                libDbContext.ChapterDownloadRecords.Update(chapterDownload);
                throw;
            }
        }


        private long CreateCbzFile(ChapterDownloadRecord chapterDownload, string chapterFolder, string seriesFolder)
        {
            var cbzFilePath = Path.Combine(seriesFolder, chapterDownload.Chapter!.GetCbzFileName());

            if (File.Exists(cbzFilePath))
            {
                File.Delete(cbzFilePath);
            }

            ZipFile.CreateFromDirectory(chapterFolder, cbzFilePath);

            try
            {
                Directory.Delete(chapterFolder, recursive: true);
                logger.LogInformation("Cleaned up extracted chapter folder: {ChapterFolder}", chapterFolder);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete chapter folder: {ChapterFolder}", chapterFolder);
            }

            logger.LogInformation("Created CBZ archive: {CbzFilePath}", cbzFilePath);

            var size = new FileInfo(cbzFilePath).Length;
            return size;
        }

        private void MoveTempCbzFilesToCollection(Manga manga)
        {
            var cbzFiles = Directory.GetFiles(manga.GetTempDirectory(), "*.cbz", SearchOption.AllDirectories);

            foreach (var cbzFile in cbzFiles)
            {
                var relativePath = Path.GetRelativePath(manga.GetTempDirectory(), cbzFile);
                var destinationPath = Path.Combine(manga.GetDirectory(), relativePath);

                var destinationDir = Path.GetDirectoryName(destinationPath);

                if (!Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                File.Copy(cbzFile, destinationPath, overwrite: true);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    File.SetUnixFileMode(destinationPath, UnixFileMode.UserRead
                                                        | UnixFileMode.UserWrite
                                                        | UnixFileMode.UserExecute
                                                        | UnixFileMode.GroupRead
                                                        | UnixFileMode.GroupWrite
                                                        | UnixFileMode.GroupExecute
                                                        | UnixFileMode.OtherRead
                                                        | UnixFileMode.OtherExecute);
                }


                logger.LogInformation("Copied: {cbzFile} → {destinationPath}", cbzFile, destinationPath);
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
}


