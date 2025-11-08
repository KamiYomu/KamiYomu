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
            DownloadStatus = DownloadStatus.Pending;
            CreateAt = DateTime.UtcNow;
        }

        public void Schedule(string backgroundJobId)
        {
            DownloadStatus = DownloadStatus.Pending;
            BackgroundJobId = backgroundJobId;
        }

        public void Pending()
        {
            DownloadStatus = DownloadStatus.Pending;
            StatusUpdateAt = DateTime.UtcNow;
        }

        public void Processing()
        {
            DownloadStatus = DownloadStatus.Completed;
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

        public string RecurringJobIdentifier()
        {
            return $"{nameof(MangaDownloadRecord)}-{Id}";
        }

        



        public Guid Id { get; private set; }
        public string BackgroundJobId { get; private set; }
        public Library Library { get; private set; }
        public DateTime CreateAt { get; private set; }
        public DateTime? StatusUpdateAt { get; private set; }
        public DownloadStatus DownloadStatus { get; private set; }
        public string CancellationReason { get; private set; }
    }
}
