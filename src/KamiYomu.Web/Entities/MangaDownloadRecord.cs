using Hangfire;
using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Entities
{
    public class MangaDownloadRecord
    {
        protected MangaDownloadRecord() { }
        public MangaDownloadRecord(Library library, string jobId)
        {
            Library = library;
            BackgroundJobId = jobId;
            DownloadStatus = DownloadStatus.ToBeRescheduled;
            StatusUpdateAt = DateTimeOffset.UtcNow;
            CreateAt = DateTimeOffset.UtcNow;
        }

        public void Schedule(string backgroundJobId)
        {
            StatusReason = null;
            DownloadStatus = DownloadStatus.Scheduled;
            BackgroundJobId = backgroundJobId;
        }

        public void Pending(string statusReason = "")
        {
            StatusReason = statusReason;
            DownloadStatus = DownloadStatus.ToBeRescheduled;
            StatusUpdateAt = DateTimeOffset.UtcNow;
        }

        public void Processing()
        {
            StatusReason = null;
            DownloadStatus = DownloadStatus.InProgress;
            StatusUpdateAt = DateTimeOffset.UtcNow;
        }
        public void Complete()
        {
            StatusReason = null;
            DownloadStatus = DownloadStatus.Completed;
            StatusUpdateAt = DateTimeOffset.UtcNow;
        }

        public void Cancelled(string statusReason)
        {

            StatusReason = statusReason;
            DownloadStatus = DownloadStatus.Cancelled;
            StatusUpdateAt = DateTimeOffset.UtcNow;
        }

        public Guid Id { get; private set; }
        public string BackgroundJobId { get; private set; }
        public Library Library { get; private set; }
        public DateTimeOffset CreateAt { get; private set; }
        public DateTimeOffset? StatusUpdateAt { get; private set; }
        public DownloadStatus DownloadStatus { get; private set; }
        public string? StatusReason { get; private set; }
    }
}
