using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Infrastructure.Storage;
using System;
using System.Xml.Linq;

namespace KamiYomu.Web.Extensions
{
    public static class ChapterExtension
    {
        public static string ToComicInfo(this Chapter chapter, Guid libraryId)
        {
            XElement comicInfo = new("ComicInfo",
                new XElement("Title",  $"{chapter?.ChapterFileName()} {chapter?.Title ?? "Untitled Chapter"}" ),
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
                new XElement("Notes", $"libraryId:{libraryId};")
            );

            return comicInfo.ToString();
        }

        public static string GetCbzFilePath(this Chapter chapter)
        {
            var mangaDirectory = chapter.ParentManga.GetDirectory();
            var cbzFileName = chapter.GetCbzFileName();
            return Path.Combine(mangaDirectory, cbzFileName);
        }

        public static string GetCbzFileName(this Chapter chapter)
        {
            string cbzFileName = $"{ChapterFileName(chapter)}.cbz";
            return cbzFileName;
        }

        public static string ChapterFileName(this Chapter chapter)
        {
            var volumePart = chapter.Volume != 0 ? $"Vol.{chapter.Volume:000} " : "";

            var chapterPart = chapter.Number > -1 ? $"Ch.{chapter.Number.ToString().PadLeft(4, '0')}"
                                                                 : $"Ch.{chapter.Id.ToString().Substring(0, 8)}";

            var cbzFileName = $"{FileNameHelper.SanitizeFileName(chapter.ParentManga.FolderName)} {volumePart}{chapterPart}";
            return cbzFileName;
        }

        public static string GetVolumeFolderName(this Chapter chapter, string seriesFolder)
        {
            return chapter.Volume != 0 ? Path.Combine(seriesFolder, $"Volume {chapter.Volume:000}")
                                       : seriesFolder;
        }

        public static string GetChapterFolderName(this Chapter chapter)
        {
            return chapter.Number != 0 ? $"Chapter {chapter.Number:0000}"
                                       : $"Chapter {chapter.Id.ToString()[..8]}";
        }

        public static string GetChapterFolderPath(this Chapter chapter, string seriesFolder)
        {
            return Path.Combine(chapter.GetVolumeFolderName(seriesFolder), chapter.GetChapterFolderName());
        }


        public static string GetCbzFileSize(this Chapter chapter)
        {
            var fileInfo = new FileInfo(chapter.GetCbzFilePath()); 
            
            if(!fileInfo.Exists)
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
