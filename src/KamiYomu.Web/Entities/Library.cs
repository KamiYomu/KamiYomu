using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using Microsoft.Extensions.Options;

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

        public Guid Id { get; private set; }
        public CrawlerAgent CrawlerAgent { get; private set; }
        public Manga Manga { get; private set; }
        public string FilePathTemplate { get; private set; }
    }
}
