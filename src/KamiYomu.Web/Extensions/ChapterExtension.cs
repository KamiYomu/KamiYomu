using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Services;
using Microsoft.Extensions.Options;
using System.Xml.Linq;

namespace KamiYomu.Web.Extensions
{
    public static class ChapterExtension
    {
        public static string ToComicInfo(this Chapter chapter, Library library)
        {
            XElement comicInfo = new("ComicInfo",
                new XElement("Title", $"{chapter?.GetCbzFileNameWithoutExtension(library)} {chapter?.Title ?? "Untitled Chapter"}"),
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
                new XElement("Notes", $"libraryId:{library.Id};")
            );

            return comicInfo.ToString();
        }

        public static string GetTempChapterDirectory(this Chapter chapter, Library library)
        {
            var specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            var filePathTemplate = library.FilePathTemplate;

            if (string.IsNullOrWhiteSpace(filePathTemplate))
            {
                filePathTemplate = specialFolderOptions.Value.FilePathFormat;
            }

            var chapterFolder = TemplateResolver.Resolve(filePathTemplate, library.Manga, chapter);

            var dirPath = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, chapterFolder);

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            return dirPath;
        }

        public static string GetCbzFilePath(this Chapter chapter, Library library)
        {
            var specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            var filePathTemplate = library.FilePathTemplate;

            if (string.IsNullOrWhiteSpace(filePathTemplate))
            {
                filePathTemplate = specialFolderOptions.Value.FilePathFormat;
            }

            var filePathTemplateResolved = TemplateResolver.Resolve(filePathTemplate, library.Manga, chapter);
            var filePath = Path.Combine(specialFolderOptions.Value.MangaDir, filePathTemplateResolved) + ".cbz";

            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return filePath;
        }

        public static string GetCbzFileName(this Chapter chapter, Library library)
        {
            var cbzFilePath = chapter.GetCbzFilePath(library);
            string cbzFileName = Path.GetFileName(cbzFilePath);
            return cbzFileName;
        }

        public static string GetCbzFileNameWithoutExtension(this Chapter chapter, Library library)
        {
            var cbzFilePath = chapter.GetCbzFilePath(library);
            string cbzFileName = Path.GetFileNameWithoutExtension(cbzFilePath);
            return cbzFileName;
        }

        public static string GetCbzFileSize(this Chapter chapter, Library library)
        {
            var fileInfo = new FileInfo(chapter.GetCbzFilePath(library));

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

    }
}
