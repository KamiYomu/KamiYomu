namespace KamiYomu.Web.Entities.Definitions;
/// <summary>
/// DownloadStatus represents the current state of a download operation, indicating whether it is pending, in progress, completed, or cancelled.
/// </summary>
public enum DownloadStatus
{
    /// <summary>
    /// To be rescheduled, the download has not started yet or needs to be retried.
    /// </summary>
    ToBeRescheduled = 0,
    /// <summary>
    /// Scheduled, the download is scheduled and waiting to be processed.
    /// </summary>
    Scheduled = 1,
    /// <summary>
    /// In progress, the download is currently being processed.
    /// </summary>
    InProgress = 2,
    /// <summary>
    /// Download completed successfully.
    /// </summary>
    Completed = 3,
    /// <summary>
    /// Download has been cancelled, either by user action or due to an error.
    /// </summary>
    Cancelled = 4,
}
