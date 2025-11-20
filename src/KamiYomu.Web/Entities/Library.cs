using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Infrastructure.Contexts;

namespace KamiYomu.Web.Entities
{
    public class Library
    {
        private LibraryDbContext _libraryDbContext;

        protected Library() { }
        public Library(CrawlerAgent agentCrawler, Manga manga)
        {
            AgentCrawler = agentCrawler;
            Manga = string.IsNullOrEmpty(manga.Title) ? null : manga;
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
            return $"{Manga!.Title}-{Id}-{AgentCrawler.Id}";
        }

        public Guid Id { get; private set; }
        public CrawlerAgent AgentCrawler { get; private set; }
        public Manga Manga { get; private set; }
    }
}
