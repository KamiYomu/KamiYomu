using KamiYomu.CrawlerAgents.Core.Catalog;
using System.Xml.Linq;

namespace KamiYomu.Web.Extensions
{
    public static class ChapterExtension
    {
        public static string ToComicInfo(this Chapter chapter)
        {
            XElement comicInfo = new("ComicInfo",
                 new XElement("Tags", string.Join(',', chapter.ParentManga.Tags)),
                 new XElement("LanguageISO", chapter.ParentManga.OriginalLanguage),
                 new XElement("Title", chapter.Title),
                 //new XElement("Writer", string.Join(',', chapter.ParentManga.Authors)),
                 new XElement("Volume", chapter.Volume),
                 new XElement("Number", chapter.Number)
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
            var volumePart = chapter.Volume != 0 ? $"Vol.{chapter.Volume:00} " : "";

            var chapterPart = chapter.Number > -1 ? $"Ch.{chapter.Number.ToString().PadLeft(3, '0')}"
                                                                 : $"Ch.{chapter.Id.ToString().Substring(0, 8)}";

            var cbzFileName = $"{chapter.ParentManga.FolderName} {volumePart}{chapterPart}.cbz";
            return cbzFileName;
        }

        public static string GetVolumeFolderName(this Chapter chapter, string seriesFolder)
        {
            return chapter.Volume != 0 ? Path.Combine(seriesFolder, $"Volume {chapter.Volume:00}")
                                       : seriesFolder;
        }

        public static string GetChapterFolderName(this Chapter chapter)
        {
            return chapter.Number != 0 ? $"Chapter {chapter.Number:000}"
                                       : $"Chapter {chapter.Id.ToString()[..8]}";
        }

        public static string GetChapterFolderPath(this Chapter chapter, string seriesFolder)
        {
            return Path.Combine(chapter.GetVolumeFolderName(seriesFolder), chapter.GetChapterFolderName());
        }

    }
}
