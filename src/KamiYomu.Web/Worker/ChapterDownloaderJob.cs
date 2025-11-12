using Hangfire.Server;
using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace KamiYomu.Web.Worker
{
    public class ChapterDownloaderJob : IChapterDownloaderJob
    {
        private readonly ILogger<ChapterDownloaderJob> _logger;
        private readonly Settings.Worker _workerOptions;
        private readonly DbContext _dbContext;
        private readonly IAgentCrawlerRepository _agentCrawlerRepository;
        private readonly HttpClient _httpClient;

        public ChapterDownloaderJob(
            ILogger<ChapterDownloaderJob> logger,
            IOptions<Settings.Worker> workerOptions,
            DbContext dbContext,
            IAgentCrawlerRepository agentCrawlerRepository,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _workerOptions = workerOptions.Value;
            _dbContext = dbContext;
            _agentCrawlerRepository = agentCrawlerRepository;
            _httpClient = httpClientFactory.CreateClient(Settings.Worker.HttpClientBackground);
        }

        public async Task DispatchAsync(Guid crawlerId, Guid libraryId, Guid mangaDownloadId, Guid chapterDownloadId, string title, PerformContext context, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Dispatch cancelled before processing chapter: {ChapterDownloadId}", chapterDownloadId);
                return;
            }

            var userPreference = _dbContext.UserPreferences.FindOne(p => true);

            Thread.CurrentThread.CurrentCulture =
            Thread.CurrentThread.CurrentUICulture =
            CultureInfo.CurrentCulture =
            CultureInfo.CurrentUICulture = userPreference?.GetCulture() ?? CultureInfo.GetCultureInfo("en-US");

            var library = _dbContext.Libraries.FindById(libraryId);
            if(library == null)
            {
                _logger.LogWarning("Dispatch \"{title}\" could not proceed — the associated library record no longer exists.", title);
                return;
            }
            using var libDbContext = library.GetDbContext();

            var mangaDownload = libDbContext.MangaDownloadRecords
                .Include(p => p.Library)
                .FindById(mangaDownloadId);

            var chapterDownload = libDbContext.ChapterDownloadRecords.FindById(chapterDownloadId);

            if (chapterDownload is null)
            {
                _logger.LogError("ChapterDownloadRecord not found: {ChapterDownloadId}", chapterDownloadId);
                return;
            }

            chapterDownload.Processing();
            libDbContext.ChapterDownloadRecords.Update(chapterDownload);

            var pages = await _agentCrawlerRepository.GetChapterPagesAsync(
                chapterDownload.CrawlerAgent,
                chapterDownload.Chapter,
                cancellationToken);

            var seriesFolder = mangaDownload.Library.Manga!.GetTempDirectory();

            var chapterFolderPath = chapterDownload.Chapter.GetChapterFolderPath(seriesFolder);

            Directory.CreateDirectory(chapterFolderPath);

            var pageCount = pages.Count();

            _logger.LogInformation("{crawler}: Downloading {Count} pages to chapter folder: {chapterFolderPath}", library.AgentCrawler.DisplayName, pageCount, chapterFolderPath);

            File.WriteAllText(Path.Join(chapterFolderPath, "ComicInfo.xml"), chapterDownload.Chapter.ToComicInfo());

            int index = 1;

            foreach (var page in pages.OrderBy(p => p.PageNumber))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("{crawler}: Dispatch cancelled during page download. Chapter: {ChapterDownloadId}", library.AgentCrawler.DisplayName, chapterDownloadId);
                    chapterDownload.Cancelled($"Dispatch cancelled during page download. Chapter: {chapterDownloadId}");
                    libDbContext.ChapterDownloadRecords.Update(chapterDownload);
                    return;
                }

                var fileName = $"{index:D3}-{Path.GetFileName(page.ImageUrl.AbsolutePath)}";
                var filePath = Path.Combine(chapterFolderPath, fileName);

                try
                {
                    var imageBytes = await _httpClient.GetByteArrayAsync(page.ImageUrl, cancellationToken);
                    await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

                    _logger.LogInformation("{crawler}: Downloaded page {Index}/{count} to {FilePath}", library.AgentCrawler.DisplayName, index, pageCount, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{crawler}: Failed to download page {Index}/{count} from {Url}", library.AgentCrawler.DisplayName,  index, pageCount, page.ImageUrl);
                }

                index++;

                await Task.Delay(_workerOptions.GetWaitPeriod(), cancellationToken);
            }

            _logger.LogInformation("{crawler}: Completed download of chapter {ChapterDownloadId} to {ChapterFolder}", library.AgentCrawler.DisplayName, chapterDownloadId, chapterFolderPath);

            CreateCbzFile(chapterDownload, chapterFolderPath, seriesFolder);

            MoveTempCbzFilesToCollection(mangaDownload!.Library!.Manga!);
            chapterDownload.Complete();
            libDbContext.ChapterDownloadRecords.Update(chapterDownload);
        }

        private void CreateCbzFile(ChapterDownloadRecord chapterDownload, string chapterFolder, string seriesFolder)
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
                _logger.LogInformation("Cleaned up extracted chapter folder: {ChapterFolder}", chapterFolder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete chapter folder: {ChapterFolder}", chapterFolder);
            }

            _logger.LogInformation("Created CBZ archive: {CbzFilePath}", cbzFilePath);
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


                _logger.LogInformation("Copied: {cbzFile} → {destinationPath}", cbzFile, destinationPath);
            }
        }

    }
}


