using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using System.Xml.Linq;

namespace KamiYomu.Web.Entities
{
    public class Library
    {
        private LibraryDbContext _libraryDbContext;

        protected Library() { }
        public Library(CrawlerAgent agentCrawler, Manga manga, string filePathTemplate)
        {
            CrawlerAgent = agentCrawler;
            Manga = string.IsNullOrEmpty(manga.Title) ? null : manga;
            FilePathTemplate = filePathTemplate;
        }

        public LibraryDbContext GetDbContext()
        {
            return _libraryDbContext ??= new LibraryDbContext(Id);
        }

        public void DropDbContext()
        {
            _libraryDbContext.DropDatabase();
        }

        public string GetDiscovertyJobId()
        {
            return $"{Manga!.Title}-{Id}-{CrawlerAgent.Id}";
        }

        public string GetTempDirectory()
        {
            var specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            var filePathTemplate = FilePathTemplate;

            if (string.IsNullOrWhiteSpace(filePathTemplate))
            {
                filePathTemplate = specialFolderOptions.Value.FilePathFormat;
            }

            var mangaFolder = TemplateResolver.Resolve(filePathTemplate, Manga, null);

            var dirPath = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, Path.GetDirectoryName(mangaFolder));

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            return dirPath;
        }


        public string GetFilePathTemplate()
        {
            var filePathTemplate = FilePathTemplate;
            if (string.IsNullOrWhiteSpace(filePathTemplate))
            {
                var specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
                filePathTemplate = specialFolderOptions.Value.FilePathFormat;
            }
            return filePathTemplate;
        }

        public string GetFilePathTemplateResolved(Chapter? chapter = null)
        {
            var filePathTemplate = GetFilePathTemplate();
            return TemplateResolver.Resolve(filePathTemplate, Manga, chapter);
        }

        public string GetCbzFilePath(Chapter chapter)
        {
            var filePathTemplate = GetFilePathTemplate();
            var filePathTemplateResolved = TemplateResolver.Resolve(filePathTemplate, Manga, chapter);
            var specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            var filePath = Path.Combine(specialFolderOptions.Value.MangaDir, filePathTemplateResolved) + ".cbz";

            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return filePath;
        }

        public string GetCbzFileName(Chapter chapter)
        {
            var cbzFilePath = GetCbzFilePath(chapter);
            string cbzFileName = Path.GetFileName(cbzFilePath);
            return cbzFileName;
        }

        public string GetCbzFileNameWithoutExtension(Chapter chapter)
        {
            var cbzFilePath = GetCbzFilePath(chapter);
            string cbzFileName = Path.GetFileNameWithoutExtension(cbzFilePath);
            return cbzFileName;
        }

        public string GetCbzFileSize(Chapter chapter)
        {
            var fileInfo = new FileInfo(GetCbzFilePath(chapter));

            if (!fileInfo.Exists)
                return I18n.NotStarted;

            long bytes = fileInfo.Length;

            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        public string GetTempChapterDirectory(Chapter chapter)
        {
            var filePathTemplate = GetFilePathTemplate();

            var chapterFolder = TemplateResolver.Resolve(filePathTemplate, Manga, chapter);

            var dirPath = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, chapterFolder);

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            return dirPath;
        }

        public string GetMangaDirectory()
        {
            var specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();

            var dirPath = Path.Combine(specialFolderOptions.Value.MangaDir, GetFilePathTemplateResolved());

            var directory = Path.GetDirectoryName(dirPath); 

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return directory;
        }

        public string ToComicInfo(Chapter chapter)
        {
            XElement comicInfo = new("ComicInfo",
                new XElement("Title", $"{GetCbzFileNameWithoutExtension(chapter)} {chapter?.Title ?? "Untitled Chapter"}"),
                new XElement("Series", chapter?.ParentManga?.Title ?? string.Empty),
                new XElement("Number", chapter?.Number.ToString() ?? string.Empty),
                new XElement("Volume", chapter?.Volume.ToString() ?? string.Empty),
                new XElement("Writer", string.Join(", ", chapter?.ParentManga?.Authors ?? [])),
                new XElement("Penciller", string.Join(", ", chapter?.ParentManga?.Artists ?? [])),
                new XElement("CoverArtist", string.Join(", ", chapter?.ParentManga?.Artists ?? [])),
                new XElement("LanguageISO", chapter?.ParentManga?.OriginalLanguage ?? string.Empty),
                new XElement("Genre", string.Join(", ", chapter?.ParentManga?.Tags ?? [])),
                new XElement("ScanInformation", "KamiYomu"),
                new XElement("Web", chapter?.Uri?.ToString() ?? chapter?.ParentManga.WebSiteUrl ?? string.Empty),
                new XElement("AgeRating", (chapter?.ParentManga?.IsFamilySafe ?? true) ? "12+" : "Mature"),
                new XElement("Notes", $"libraryId:{Id};")
            );

            return comicInfo.ToString();
        }

        public Guid Id { get; private set; }
        public CrawlerAgent CrawlerAgent { get; private set; }
        public Manga Manga { get; private set; }
        public string FilePathTemplate { get; private set; }
    }
}
