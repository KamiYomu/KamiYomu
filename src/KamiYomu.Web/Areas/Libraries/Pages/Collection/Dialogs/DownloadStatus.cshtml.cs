using Hangfire;
using Hangfire.Storage;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyModel;

namespace KamiYomu.Web.Areas.Libraries.Pages.Mangas.Dialogs;

public class DownloadStatusModel(DbContext dbContext, INotificationService notificationService) : PageModel
{
    [BindProperty]
    public FollowButtonViewModel FollowButtonViewModel { get; set; }
    public Entities.Library Library { get; set; }
    public decimal Completed { get; set; } = 0;
    public decimal Total { get; set; } = 0;
    public decimal Progress { get; set; } = 0;
    public MangaDownloadRecord Record { get; set; } = null;


    public void OnGet(Guid libraryId)
    {
        FollowButtonViewModel = new FollowButtonViewModel
        {
            IsFollowing = false,
            LibraryId = libraryId
        };

        Library = dbContext.Libraries.FindOne(p => p.Id == libraryId);
        using var libDbContext = Library.GetDbContext();

        var downloadManga = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == Library.Id);

        if (downloadManga == null) return;
        using var connection = JobStorage.Current.GetConnection();
        var recurringJobs = connection.GetRecurringJobs();

        Record = downloadManga;
        FollowButtonViewModel.IsFollowing = recurringJobs.Any(job => string.Equals(job.Id, Library.GetDiscovertyJobId(), StringComparison.OrdinalIgnoreCase));
        var downloadChapters = libDbContext.ChapterDownloadRecords.Find(p => p.MangaDownload.Id == downloadManga.Id).OrderBy(p => p.Chapter.Number).ToList();
        Completed = downloadChapters.Count(p => p.DownloadStatus == DownloadStatus.Completed);
        Total = downloadChapters.Count;
        if (Total > 0)
        {
            Progress = (Completed / Total) * 100;
        }
        else
        {
            Progress = 0;
        }

    }

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
            RecurringJob.AddOrUpdate<IChapterDiscoveryJob>(
            Library.GetDiscovertyJobId(),
            AppOptions.Defaults.Worker.DiscoveryNewChapterQueues,
            (job) => job.DispatchAsync(Library.AgentCrawler.Id, Library.Id, null!, CancellationToken.None),
            Cron.Daily());

            await notificationService.PushSuccessAsync(I18n.YouStartedFollowingThisTitle, cancellationToken);
        }

        FollowButtonViewModel.IsFollowing = !FollowButtonViewModel.IsFollowing;

        return Partial("_FollowButton", FollowButtonViewModel);
    }

}

public class FollowButtonViewModel 
{
    [BindProperty]
    public bool IsFollowing { get; set; }
    [BindProperty]
    public Guid LibraryId { get; set; }
}
