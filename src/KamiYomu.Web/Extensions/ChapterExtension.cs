using System.Xml.Linq;

namespace KamiYomu.Web.Extensions
{
    public static class ChapterExtension
    {
        public static string ToComicInfo(this CrawlerAgents.Core.Catalog.Chapter chapter)
        {
            XElement comicInfo = new("ComicInfo",
                 new XElement("Tags", string.Join(',', chapter.ParentManga.Tags)),
                 new XElement("LanguageISO", chapter.ParentManga.OriginalLanguage),
                 new XElement("Title", chapter.Title),
                 //new XElement("Writer", string.Join(',', chapter.aut)),
                 new XElement("Volume", chapter.Volume),
                 new XElement("Number", chapter.Number)
            );
            return comicInfo.ToString();
        }
    }
}
