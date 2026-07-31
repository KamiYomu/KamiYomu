using KamiYomu.Web.Entities;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;
/// <summary>
/// 
/// </summary>
public interface IWorkerService
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="mangaDownloadRecord"></param>
    /// <returns></returns>
    string ScheduleMangaDownload(MangaDownloadRecord mangaDownloadRecord);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="mangaDownloadRecord"></param>
    void CancelMangaDownload(MangaDownloadRecord mangaDownloadRecord);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="library"></param>
    void RemoveDiscoverRecurringJob(Library library);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="library"></param>
    void ScheduleDiscoverRecurringJob(Library library);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="library"></param>
    /// <returns></returns>
    string TriggerDiscoverRecurringJob(Library library);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="library"></param>
    /// <returns></returns>
    bool IsDiscoverRecurringJobScheduled(Library library);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="library"></param>
    /// <returns></returns>
    bool IsDiscoverRecurringJobRunning(Library library);
}
