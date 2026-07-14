using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities.Definitions;

namespace KamiYomu.Web.Entities;

/// <summary>
/// Represents a download record for a single chapter.
/// Tracks the associated crawler agent, parent manga download record, chapter metadata,
/// background job id, current download status and timestamps.
/// </summary>
public class ChapterDownloadRecord
{
    /// <summary>
    /// Protected parameterless constructor for deserialization or ORM.
    /// </summary>
    protected ChapterDownloadRecord() { }

    /// <summary>
    /// Creates a new chapter download record and initializes status and timestamps.
    /// </summary>
    /// <param name="agentCrawler">The crawler agent responsible for this chapter.</param>
    /// <param name="mangaDownload">The parent manga download record.</param>
    /// <param name="chapter">The chapter metadata associated with this record.</param>
    public ChapterDownloadRecord(CrawlerAgent agentCrawler, MangaDownloadRecord mangaDownload, Chapter chapter)
    {
        CrawlerAgent = agentCrawler;
        MangaDownload = mangaDownload;
        Chapter = chapter;
        DownloadStatus = DownloadStatus.ToBeRescheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
        CreateAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks this record to be rescheduled and sets an optional reason.
    /// </summary>
    /// <param name="statusReason">Optional reason for rescheduling. Default is empty string.</param>
    /// <remarks>Called when the download should be retried at a later time.</remarks>
    public void ToBeRescheduled(string statusReason = "")
    {
        StatusReason = statusReason;
        DownloadStatus = DownloadStatus.ToBeRescheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks this record as scheduled and records the background job id.
    /// </summary>
    /// <param name="jobId">The background job identifier.</param>
    /// <remarks>Called when the download has been queued for background processing.</remarks>
    public void Scheduled(string jobId)
    {
        BackgroundJobId = jobId;
        StatusReason = null;
        DownloadStatus = DownloadStatus.Scheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks this record as currently being processed.
    /// </summary>
    /// <remarks>Clears any existing status reason and updates the status timestamp.</remarks>
    public void Processing()
    {
        StatusReason = null;
        DownloadStatus = DownloadStatus.InProgress;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks this record as completed.
    /// </summary>
    public void Complete()
    {
        StatusReason = null;
        DownloadStatus = DownloadStatus.Completed;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Cancels this record and records a reason.
    /// </summary>
    /// <param name="statusReason">The reason why the download was cancelled.</param>
    public void Cancelled(string statusReason)
    {
        StatusReason = statusReason;
        BackgroundJobId = string.Empty;
        DownloadStatus = DownloadStatus.Cancelled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Determines whether this record should be run (rescheduled or scheduled).
    /// </summary>
    /// <returns>True when status is ToBeRescheduled or Scheduled.</returns>
    public bool ShouldRun()
    {
        return DownloadStatus is DownloadStatus.ToBeRescheduled or DownloadStatus.Scheduled;
    }

    /// <summary>
    /// Determines whether the download can be cancelled in its current state.
    /// </summary>
    /// <returns>True for InProgress, Scheduled or ToBeRescheduled statuses.</returns>
    public bool IsCancellable()
    {
        return DownloadStatus is DownloadStatus.InProgress or DownloadStatus.Scheduled or DownloadStatus.ToBeRescheduled;
    }

    /// <summary>
    /// Indicates whether the download is actively in progress.
    /// </summary>
    /// <returns>True when not stale or when status is Scheduled.</returns>
    public bool IsInProgress()
    {
        return !IsStale() || DownloadStatus == DownloadStatus.Scheduled;
    }

    /// <summary>
    /// Determines whether the in-progress download is stale (has not updated for over a day).
    /// </summary>
    /// <returns>True when status is InProgress and last status update is more than 24 hours ago.</returns>
    public bool IsStale()
    {
        return DownloadStatus == DownloadStatus.InProgress
               && StatusUpdateAt < DateTimeOffset.UtcNow.AddDays(-1);
    }

    /// <summary>
    /// Indicates whether the download has been cancelled.
    /// </summary>
    public bool IsCancelled()
    {
        return DownloadStatus == DownloadStatus.Cancelled;
    }

    /// <summary>
    /// Determines whether the download can be rescheduled (completed or cancelled).
    /// </summary>
    public bool IsReschedulable()
    {
        return IsCompleted() || IsCancelled();
    }

    /// <summary>
    /// Indicates whether the download is completed.
    /// </summary>
    public bool IsCompleted()
    {
        return DownloadStatus == DownloadStatus.Completed;
    }

    /// <summary>
    /// Returns the total whole days since the last status update.
    /// </summary>
    /// <returns>Number of days since last status update or int.MaxValue when unknown.</returns>
    public int LastUpdatedStatusTotalDays()
    {
        return !StatusUpdateAt.HasValue ? int.MaxValue : (int)(DateTimeOffset.UtcNow - StatusUpdateAt.Value).TotalDays;
    }

    /// <summary>
    /// Deletes the downloaded CBZ file for the chapter if it exists in the given library.
    /// </summary>
    /// <param name="library">Library instance used to resolve the file path.</param>
    public void DeleteDownloadedFileIfExists(Library library)
    {
        if (IsDownloadedFileExists(library))
        {
            string path = library.GetCbzFilePath(Chapter);
            File.Delete(path);
        }
    }

    /// <summary>
    /// Checks whether the downloaded CBZ file for the chapter exists in the given library.
    /// </summary>
    /// <param name="library">Library instance used to resolve the file path.</param>
    /// <returns>True if the file exists; otherwise false.</returns>
    public bool IsDownloadedFileExists(Library library)
    {
        string path = library.GetCbzFilePath(Chapter);

        return File.Exists(path);
    }

    /// <summary>
    /// Unique identifier for the chapter download record.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The crawler agent responsible for downloading this chapter.
    /// </summary>
    public CrawlerAgent CrawlerAgent { get; private set; }

    /// <summary>
    /// Parent manga download record that groups chapter downloads.
    /// </summary>
    public MangaDownloadRecord MangaDownload { get; private set; }

    /// <summary>
    /// Chapter metadata associated with this download record.
    /// </summary>
    public Chapter Chapter { get; private set; }

    /// <summary>
    /// Background job identifier associated with this download (if scheduled).
    /// </summary>
    public string BackgroundJobId { get; private set; }

    /// <summary>
    /// Creation timestamp for this record (UTC).
    /// </summary>
    public DateTimeOffset CreateAt { get; private set; }

    /// <summary>
    /// Timestamp of the last status update (UTC).
    /// </summary>
    public DateTimeOffset? StatusUpdateAt { get; private set; }

    /// <summary>
    /// Current download status.
    /// </summary>
    public DownloadStatus DownloadStatus { get; private set; }

    /// <summary>
    /// Optional human-readable reason for the current status.
    /// </summary>
    public string? StatusReason { get; private set; }
}
