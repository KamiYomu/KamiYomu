using KamiYomu.CrawlerAgents.Core.Catalog;

namespace KamiYomu.Web.Extensions
{
    public static class MangaExtensions
    {
        public static string GetTempDirectory(this Manga manga)
        {
            var dirPath = Path.Combine(Path.GetTempPath(), manga.FolderName);

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            return dirPath;
        }

        public static string GetDirectory(this Manga manga)
        {
            var dirPath = Path.Combine(Settings.SpecialFolders.MangaDir, manga.FolderName);

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            return dirPath;
        }
    }
}
