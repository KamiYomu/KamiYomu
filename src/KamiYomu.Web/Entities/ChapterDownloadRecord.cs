using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Extensions;

namespace KamiYomu.Web.Entities
{
    public class ChapterDownloadRecord
    {
        protected ChapterDownloadRecord() { }
        public ChapterDownloadRecord(CrawlerAgent agentCrawler, MangaDownloadRecord mangaDownload, Chapter chapter)
        {
            CrawlerAgent = agentCrawler;
            MangaDownload = mangaDownload;
            Chapter = chapter;
            DownloadStatus = DownloadStatus.Pending;
            StatusUpdateAt = DateTime.UtcNow;
            CreateAt = DateTime.UtcNow;
        }

        public void Pending(string statusReason = "")
        {
            StatusReason = statusReason;
            DownloadStatus = DownloadStatus.Pending;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public void Scheduled(string jobId)
        {
            BackgroundJobId = jobId;
            StatusReason = null;
            DownloadStatus = DownloadStatus.Scheduled;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public void Processing()
        {
            StatusReason = null;
            DownloadStatus = DownloadStatus.InProgress;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public void Complete()
        {
            StatusReason = null;
            DownloadStatus = DownloadStatus.Completed;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public void Cancelled(string statusReason)
        {
            StatusReason = statusReason;
            BackgroundJobId = string.Empty;
            DownloadStatus = DownloadStatus.Cancelled;
            StatusUpdateAt = DateTime.UtcNow;
        }


        public bool IsInProgress()
        {
            return DownloadStatus == DownloadStatus.Scheduled || DownloadStatus == DownloadStatus.InProgress;
        }

        public bool IsCancelled()
        {
            return DownloadStatus == DownloadStatus.Cancelled;
        }   

        public bool IsCompleted()
        {
            return DownloadStatus == DownloadStatus.Completed;
        }

        public int LastUpdatedStatusTotalDays()
        {
            if (!StatusUpdateAt.HasValue) return int.MaxValue;
            return (int)(DateTime.UtcNow - StatusUpdateAt.Value).TotalDays;
        }

        public void DeleteDownloadedFileIfExists()
        {
            if(IsDownloadedFileExists())
            {
                var path = Chapter.GetCbzFilePath();
                File.Delete(path);
            }
        }

        public bool IsDownloadedFileExists()
        {
            var path = Chapter.GetCbzFilePath();

            return File.Exists(path);
        }

        public Guid Id { get; private set; }
        public CrawlerAgent CrawlerAgent { get; private set; }
        public MangaDownloadRecord MangaDownload { get; private set; }
        public Chapter Chapter { get; private set; }
        public string BackgroundJobId { get; private set; }
        public DateTime CreateAt { get; private set; }
        public DateTime? StatusUpdateAt { get; private set; }
        public DownloadStatus DownloadStatus { get; private set; }
        public string? StatusReason { get; private set; }
    }
}
