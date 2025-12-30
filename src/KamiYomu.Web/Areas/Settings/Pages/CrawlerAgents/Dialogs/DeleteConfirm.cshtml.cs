using Hangfire;

using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Dialogs;

public class DeleteConfirmModel(DbContext dbContext, IBackgroundJobClient jobClient, INotificationService notificationService) : PageModel
{

    [BindProperty]
    public Guid Id { get; set; }

    public Entities.CrawlerAgent? Agent { get; set; }

    public IActionResult OnGet(Guid id)
    {
        Agent = dbContext.CrawlerAgents.FindById(id);
        return Agent == null ? NotFound() : Page();
    }

    public IActionResult OnPostAsync(CancellationToken cancellationToken)
    {
        Entities.CrawlerAgent agentCrawler = dbContext.CrawlerAgents.FindById(Id);
        IEnumerable<Entities.Library> libraries = dbContext.Libraries.Find(p => p.CrawlerAgent.Id == agentCrawler.Id);

        foreach (Entities.Library lib in libraries)
        {
            using LibraryDbContext libDbContext = lib.GetDbContext();
            IEnumerable<Entities.MangaDownloadRecord> downloadMangas = libDbContext.MangaDownloadRecords.Find(p => p.Library.CrawlerAgent.Id == agentCrawler.Id);
            IEnumerable<Entities.ChapterDownloadRecord> downloadChapters = libDbContext.ChapterDownloadRecords.Find(p => p.CrawlerAgent.Id == agentCrawler.Id);

            foreach (string? jobId in downloadMangas.Select(p => p.BackgroundJobId).Union(downloadChapters.Select(p => p.BackgroundJobId)))
            {
                _ = jobClient.Delete(jobId);
            }

            _ = libDbContext.MangaDownloadRecords.DeleteMany(p => p.Library.CrawlerAgent.Id == agentCrawler.Id);
            _ = libDbContext.ChapterDownloadRecords.DeleteMany(p => p.CrawlerAgent.Id == agentCrawler.Id);
        }

        _ = dbContext.CrawlerAgents.Delete(Id);

        List<Entities.CrawlerAgent> crawlerAgents = [.. dbContext.CrawlerAgents.FindAll()];

        _ = notificationService.PushSuccessAsync(I18n.CrawlerAgentRemovedSuccessfully, cancellationToken);

        return Partial("_CrawlerAgentList", crawlerAgents);
    }
}
