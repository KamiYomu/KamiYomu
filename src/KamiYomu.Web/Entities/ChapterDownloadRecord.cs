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
    /// <param name="crawlerAgent">The crawler agent responsible for this chapter.</param>
    /// <param name="mangaDownload">The parent manga download record.</param>
    /// <param name="chapter">The chapter metadata associated with this record.</param>
    public ChapterDownloadRecord(CrawlerAgent crawlerAgent, MangaDownloadRecord mangaDownload, Chapter chapter)
    {
        CrawlerAgent = crawlerAgent;
        MangaDownload = mangaDownload;
        Chapter = chapter;
        DownloadStatus = DownloadStatus.ToBeRescheduled;
        StatusUpdateAt = DateTimeOffset.UtcNow;
        CreateAt = DateTimeOffset.UtcNow;
    }
    /// <summary>
    /// Changes the chapter metadata associated with this record.
    /// </summary>
    /// <param name="chapter">Chapter metadata to associate with this chapter download record.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public void UpdateChapterInformation(Chapter chapter)
    {
        if (chapter.Id != Chapter?.Id)
        {
            throw new InvalidOperationException($"Cannot update Chapter with a different Id. Current Chapter Id: {Chapter.Id}, New Chapter Id: {chapter.Id}");
        }
        Chapter = chapter;
    }

    /// <summary>
    /// Changes the parent manga download record associated with this chapter download record.
    /// </summary>
    /// <param name="mangaDownload">Manga download record to associate with this chapter download record.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public void UpdateMangaDownloadInformation(MangaDownloadRecord mangaDownload)
    {
        if (mangaDownload.Id != MangaDownload?.Id)
        {
            throw new InvalidOperationException($"Cannot update MangaDownload with a different Id. Current MangaDownload Id: {MangaDownload.Id}, New MangaDownload Id: {mangaDownload.Id}");
        }
        MangaDownload = mangaDownload;
    }
    /// <summary>
    /// Updates the crawler agent information associated with this chapter download record.
    /// </summary>
    /// <param name="crawlerAgent">Crawler agent to associate with this chapter download record.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public void UpdateCrawlerAgentInformation(CrawlerAgent crawlerAgent)
    {
        if (crawlerAgent.AssemblyName != CrawlerAgent?.AssemblyName)
        {
            throw new InvalidOperationException($"Cannot update CrawlerAgent with a different AssemblyName. Current CrawlerAgent AssemblyName: {CrawlerAgent.AssemblyName}, New CrawlerAgent AssemblyName: {crawlerAgent.AssemblyName}");
        }
        CrawlerAgent = crawlerAgent;
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
    /// Indicates whether the download is scheduled.
    /// </summary>
    /// <returns>True when the download status is Scheduled; otherwise false.</returns>
    public bool IsScheduled()
    {
        return DownloadStatus == DownloadStatus.Scheduled;
    }

    /// <summary>
    /// Indicates whether the download is to be rescheduled.
    /// </summary>
    /// <returns>True when the download status is ToBeRescheduled; otherwise false.</returns>
    public bool IsToBeRescheduled()
    {
        return DownloadStatus == DownloadStatus.ToBeRescheduled;
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
