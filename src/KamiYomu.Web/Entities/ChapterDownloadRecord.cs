using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Entities
{
    public class ChapterDownloadRecord
    {
        protected ChapterDownloadRecord() { }
        public ChapterDownloadRecord(CrawlerAgent? agentCrawler, MangaDownloadRecord? mangaDownload, Chapter? chapter)
        {
            AgentCrawler = agentCrawler;
            MangaDownload = mangaDownload;
            Chapter = chapter;
            DownloadStatus = DownloadStatus.Pending;
            CreateAt = DateTime.UtcNow;
        }


        public void Scheduled(string jobId)
        {
            BackgroundJobId = jobId;
            DownloadStatus = DownloadStatus.Scheduled;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public void Processing()
        {
            DownloadStatus = DownloadStatus.Processing;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public void Complete()
        {
            DownloadStatus = DownloadStatus.Completed;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public void Cancelled(string cancellationReason)
        {
            DownloadStatus = DownloadStatus.Cancelled;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public Guid Id { get; private set; }
        public CrawlerAgent? AgentCrawler { get; private set; }
        public MangaDownloadRecord? MangaDownload { get; private set; }
        public Chapter? Chapter { get; private set; }
        public string BackgroundJobId { get; private set; }
        public DateTime CreateAt { get; private set; }
        public DateTime? StatusUpdateAt { get; private set; }
        public DownloadStatus DownloadStatus { get; private set; }
        public string CancellationReason { get; private set; }
    }
}
