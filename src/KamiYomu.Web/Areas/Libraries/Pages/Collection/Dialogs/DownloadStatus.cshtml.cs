using Hangfire;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;
/// <summary>
/// 
/// </summary>
/// <param name="workerOptions"></param>
/// <param name="dbContext"></param>
/// <param name="notificationService"></param>
/// <param name="workerService"></param>
public class DownloadStatusModel(IOptions<WorkerOptions> workerOptions,
                                 DbContext dbContext,
                                 INotificationService notificationService,
                                 IWorkerService workerService) : PageModel
{
    [BindProperty]
    public required FollowButtonViewModel FollowButtonViewModel { get; set; }
    [BindProperty]
    public required ScanNowButtonViewModel ScanNowButtonViewModel { get; set; }
    public required Entities.Library Library { get; set; }
    public MangaDownloadRecord? Record { get; set; } = null;


    public void OnGet(Guid libraryId)
    {
        FollowButtonViewModel = new FollowButtonViewModel
        {
            IsFollowing = false,
            LibraryId = libraryId
        };

        ScanNowButtonViewModel = new ScanNowButtonViewModel
        {
            IsScanning = false,
            LibraryId = libraryId
        };

        Library = dbContext.Libraries.FindOne(p => p.Id == libraryId);
        using LibraryDbContext libDbContext = Library.GetReadOnlyDbContext();

        MangaDownloadRecord downloadManga = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == Library.Id);

        if (downloadManga == null)
        {
            return;
        }

        Record = downloadManga;
        ScanNowButtonViewModel.IsScanning = workerService.IsDiscoverRecurringJobRunning(Library);
        FollowButtonViewModel.IsFollowing = workerService.IsDiscoverRecurringJobScheduled(Library);
        List<ChapterDownloadRecord> downloadChapters = [.. libDbContext.ChapterDownloadRecords.Find(p => p.MangaDownload.Id == downloadManga.Id).OrderBy(p => p.Chapter.Number)];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IActionResult> OnPostToggleFollowingAsync(CancellationToken cancellationToken)
    {
        Library = dbContext.Libraries.FindOne(p => p.Id == FollowButtonViewModel.LibraryId);
        if (FollowButtonViewModel.IsFollowing)
        {
            RecurringJob.RemoveIfExists(Library.GetDiscovertyJobId());
            await notificationService.PushSuccessAsync(I18n.YouAreNoLongerFollowingThisTitle, cancellationToken);
        }
        else
        {
            workerService.ScheduleDiscoverRecurringJob(Library);

            await notificationService.PushSuccessAsync(I18n.YouStartedFollowingThisTitle, cancellationToken);
        }

        FollowButtonViewModel.IsFollowing = !FollowButtonViewModel.IsFollowing;

        return ViewComponent("FollowButton", FollowButtonViewModel);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IActionResult OnPostStartDiscoverChaptersJob()
    {
        Library = dbContext.Libraries.FindOne(p => p.Id == ScanNowButtonViewModel.LibraryId);

        string jobId = workerService.TriggerDiscoverRecurringJob(Library);

        ScanNowButtonViewModel.IsScanning = !string.IsNullOrWhiteSpace(jobId);

        if (ScanNowButtonViewModel.IsScanning)
        {
            _ = notificationService.PushSuccessAsync(I18n.StartSearchingForChapters, CancellationToken.None);
        }

        return ViewComponent("ScanNowButton", ScanNowButtonViewModel);
    }

}
/// <summary>
/// 
/// </summary>
public class FollowButtonViewModel
{
    [BindProperty]
    public bool IsFollowing { get; set; }
    [BindProperty]
    public Guid LibraryId { get; set; }
}


public class ScanNowButtonViewModel
{
    [BindProperty]
    public bool IsScanning { get; set; }
    [BindProperty]
    public Guid LibraryId { get; set; }
}
