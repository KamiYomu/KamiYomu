using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Storage;
using Microsoft.Extensions.Options;
namespace KamiYomu.Web.Extensions
{
    public static class MangaExtensions
    {
        public static string GetTempDirectory(this Manga manga)
        {
            var dirPath = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, FileNameHelper.SanitizeFileName(manga.FolderName));

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            return dirPath;
        }

        public static string GetDirectory(this Manga manga)
        {
            var specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();

            var dirPath = Path.Combine(specialFolderOptions.Value.MangaDir, FileNameHelper.SanitizeFileName(manga.FolderName));

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            return dirPath;
        }
    }
}
