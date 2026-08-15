using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Entities;
/// <summary>
/// 
/// </summary>
public class MangaDownloadRecord
{
    protected MangaDownloadRecord() { }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="library"></param>
    /// <param name="jobId"></param>
    public MangaDownloadRecord(Library library, string jobId)
    {
        Library = library;
        BackgroundJobId = jobId;
        DownloadStatus = DownloadStatus.ToBeRescheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
        CreateAt = DateTimeOffset.UtcNow;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="library"></param>
    public void UpdateLibraryInformation(Library library)
    {
        if (library.Id != Library?.Id)
        {
            throw new InvalidOperationException($"Cannot update Library with a different Id. Current Library Id: {Library.Id}, New Library Id: {library.Id}");
        }

        Library = library;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="reason"></param>
    internal void ToBeRescheduled(string reason)
    {
        StatusReason = reason;
        DownloadStatus = DownloadStatus.ToBeRescheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="backgroundJobId"></param>
    /// <param name="statusReason"></param>
    public void Schedule(string backgroundJobId, string? statusReason = null)
    {
        StatusReason = statusReason;
        DownloadStatus = DownloadStatus.Scheduled;
        BackgroundJobId = backgroundJobId;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="statusReason"></param>
    public void Pending(string statusReason = "")
    {
        StatusReason = statusReason;
        DownloadStatus = DownloadStatus.ToBeRescheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }
    /// <summary>
    /// 
    /// </summary>
    public void Processing()
    {
        StatusReason = null;
        DownloadStatus = DownloadStatus.InProgress;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }
    /// <summary>
    /// 
    /// </summary>
    public void Complete()
    {
        StatusReason = null;
        DownloadStatus = DownloadStatus.Completed;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="statusReason"></param>
    public void Cancelled(string statusReason)
    {

        StatusReason = statusReason;
        DownloadStatus = DownloadStatus.Cancelled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }
    /// <summary>
    /// Determines whether the download record should be run.
    /// </summary>
    /// <returns>True if the download record should be run; otherwise, false.</returns>
    public bool ShouldRun()
    {
        return DownloadStatus is DownloadStatus.ToBeRescheduled or DownloadStatus.Scheduled || IsStale();
    }
    /// <summary>
    /// Determines whether the download record is stale.
    /// </summary>
    /// <returns>True if the download record is stale; otherwise, false.</returns>
    public bool IsStale()
    {
        return DownloadStatus == DownloadStatus.InProgress
               && StatusUpdateAt < DateTimeOffset.UtcNow.AddDays(-1);
    }

    /// <summary>
    /// Indicates whether the download is actively in progress.
    /// </summary>
    /// <returns>True when not stale or when status is Scheduled.</returns>
    public bool IsInProgress()
    {
        switch (DownloadStatus)
        {
            case DownloadStatus.Scheduled:
                return true;

            case DownloadStatus.InProgress:
                return !IsStale();
            default:
                return false;
        }
    }



    public Guid Id { get; private set; }
    public string BackgroundJobId { get; private set; }
    public Library Library { get; private set; }
    public DateTimeOffset CreateAt { get; private set; }
    public DateTimeOffset? StatusUpdateAt { get; private set; }
    public DownloadStatus DownloadStatus { get; private set; }
    public string? StatusReason { get; private set; }
}
