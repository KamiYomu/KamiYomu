using Hangfire;
using Hangfire.Storage;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Libraries.Pages.Collection.Dialogs;

public class DownloadStatusModel(IOptions<WorkerOptions> workerOptions,
                                 DbContext dbContext,
                                 INotificationService notificationService) : PageModel
{
    [BindProperty]
    public required FollowButtonViewModel FollowButtonViewModel { get; set; }
    public required Entities.Library Library { get; set; }
    public MangaDownloadRecord? Record { get; set; } = null;


    public void OnGet(Guid libraryId)
    {
        FollowButtonViewModel = new FollowButtonViewModel
        {
            IsFollowing = false,
            LibraryId = libraryId
        };

        Library = dbContext.Libraries.FindOne(p => p.Id == libraryId);
        using LibraryDbContext libDbContext = Library.GetReadOnlyDbContext();

        MangaDownloadRecord downloadManga = libDbContext.MangaDownloadRecords.FindOne(p => p.Library.Id == Library.Id);

        if (downloadManga == null)
        {
            return;
        }

        using IStorageConnection connection = JobStorage.Current.GetConnection();
        List<RecurringJobDto> recurringJobs = connection.GetRecurringJobs();

        Record = downloadManga;
        FollowButtonViewModel.IsFollowing = recurringJobs.Any(job => string.Equals(job.Id, Library.GetDiscovertyJobId(), StringComparison.OrdinalIgnoreCase));
        List<ChapterDownloadRecord> downloadChapters = [.. libDbContext.ChapterDownloadRecords.Find(p => p.MangaDownload.Id == downloadManga.Id).OrderBy(p => p.Chapter.Number)];
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
            string? queue = workerOptions.Value.DiscoveryNewChapterQueues.FirstOrDefault();
            RecurringJob.AddOrUpdate<IChapterDiscoveryJob>(
            Library.GetDiscovertyJobId(),
            queue,
            (job) => job.DispatchAsync(queue, Library.CrawlerAgent.Id, Library.Id, null!, CancellationToken.None),
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
