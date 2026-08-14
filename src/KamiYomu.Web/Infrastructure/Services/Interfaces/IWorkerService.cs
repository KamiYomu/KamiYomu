using KamiYomu.Web.Entities;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

/// <summary>
/// Service for managing background worker jobs including manga downloads and library discovery operations.
/// </summary>
public interface IWorkerService
{
    /// <summary>
    /// Schedules a manga download job for the specified download record.
    /// </summary>
    /// <param name="mangaDownloadRecord">The manga download record containing download details.</param>
    /// <returns>The job ID of the scheduled download.</returns>
    string ScheduleMangaDownload(MangaDownloadRecord mangaDownloadRecord);

    /// <summary>
    /// Cancels a previously scheduled manga download job.
    /// </summary>
    /// <param name="mangaDownloadRecord">The manga download record to cancel.</param>
    void CancelMangaDownload(MangaDownloadRecord mangaDownloadRecord);

    /// <summary>
    /// Removes a recurring discovery job for the specified library.
    /// </summary>
    /// <param name="library">The library whose recurring discovery job should be removed.</param>
    void RemoveDiscoverRecurringJob(Library library);

    /// <summary>
    /// Schedules a recurring discovery job for the specified library.
    /// </summary>
    /// <param name="library">The library for which to schedule the recurring discovery job.</param>
    void ScheduleDiscoverRecurringJob(Library library);

    /// <summary>
    /// Triggers an immediate execution of the discovery job for the specified library.
    /// </summary>
    /// <param name="library">The library to discover.</param>
    /// <returns>The job ID of the triggered discovery job.</returns>
    string TriggerDiscoverRecurringJob(Library library);

    /// <summary>
    /// Determines whether a recurring discovery job is currently scheduled for the specified library.
    /// </summary>
    /// <param name="library">The library to check.</param>
    /// <returns>True if a recurring discovery job is scheduled; otherwise, false.</returns>
    bool IsDiscoverRecurringJobScheduled(Library library);

    /// <summary>
    /// Determines whether a discovery job is currently running for the specified library.
    /// </summary>
    /// <param name="library">The library to check.</param>
    /// <returns>True if a discovery job is currently running; otherwise, false.</returns>
    bool IsDiscoverRecurringJobRunning(Library library);
}
