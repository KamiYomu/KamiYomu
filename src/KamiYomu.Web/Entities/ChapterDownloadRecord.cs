using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Entities;

public class ChapterDownloadRecord
{
    protected ChapterDownloadRecord() { }
    public ChapterDownloadRecord(CrawlerAgent agentCrawler, MangaDownloadRecord mangaDownload, Chapter chapter)
    {
        CrawlerAgent = agentCrawler;
        MangaDownload = mangaDownload;
        Chapter = chapter;
        DownloadStatus = DownloadStatus.ToBeRescheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
        CreateAt = DateTimeOffset.UtcNow;
    }

    public void ToBeRescheduled(string statusReason = "")
    {
        StatusReason = statusReason;
        DownloadStatus = DownloadStatus.ToBeRescheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }

    public void Scheduled(string jobId)
    {
        BackgroundJobId = jobId;
        StatusReason = null;
        DownloadStatus = DownloadStatus.Scheduled;
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
        BackgroundJobId = string.Empty;
        DownloadStatus = DownloadStatus.Cancelled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }

    public bool ShouldRun()
    {
        return DownloadStatus is DownloadStatus.ToBeRescheduled or DownloadStatus.Scheduled;
    }

    public bool IsInProgress()
    {
        return DownloadStatus == DownloadStatus.InProgress ? !IsStale() : DownloadStatus == DownloadStatus.Scheduled;
    }

    public bool IsStale()
    {
        return DownloadStatus == DownloadStatus.InProgress
               && StatusUpdateAt < DateTimeOffset.UtcNow.AddDays(-1);
    }

    public bool IsCancelled()
    {
        return DownloadStatus == DownloadStatus.Cancelled;
    }

    public bool IsReschedulable()
    {
        return IsCompleted() || IsCancelled();
    }

    public bool IsCompleted()
    {
        return DownloadStatus == DownloadStatus.Completed;
    }

    public int LastUpdatedStatusTotalDays()
    {
        return !StatusUpdateAt.HasValue ? int.MaxValue : (int)(DateTimeOffset.UtcNow - StatusUpdateAt.Value).TotalDays;
    }

    public void DeleteDownloadedFileIfExists(Library library)
    {
        if (IsDownloadedFileExists(library))
        {
            string path = library.GetCbzFilePath(Chapter);
            File.Delete(path);
        }
    }

    public bool IsDownloadedFileExists(Library library)
    {
        string path = library.GetCbzFilePath(Chapter);

        return File.Exists(path);
    }

    public Guid Id { get; private set; }
    public CrawlerAgent CrawlerAgent { get; private set; }
    public MangaDownloadRecord MangaDownload { get; private set; }
    public Chapter Chapter { get; private set; }
    public string BackgroundJobId { get; private set; }
    public DateTimeOffset CreateAt { get; private set; }
    public DateTimeOffset? StatusUpdateAt { get; private set; }
    public DownloadStatus DownloadStatus { get; private set; }
    public string? StatusReason { get; private set; }
}
