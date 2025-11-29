using Hangfire;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Pages.CrawlerAgents.Dialogs
{
    public class DeleteConfirmModel(DbContext dbContext, IBackgroundJobClient jobClient, INotificationService notificationService) : PageModel
    {

        [BindProperty]
        public Guid Id { get; set; }

        public Entities.CrawlerAgent? Agent { get; set; }

        public IActionResult OnGet(Guid id)
        {
            Agent = dbContext.CrawlerAgents.FindById(id);
            if (Agent == null) return NotFound();
            return Page();
        }

        public IActionResult OnPostAsync(CancellationToken cancellationToken)
        {
            var agentCrawler = dbContext.CrawlerAgents.FindById(Id);
            var libraries = dbContext.Libraries.Find(p => p.CrawlerAgent.Id == agentCrawler.Id);

            foreach (var lib in libraries)
            {
                using var libDbContext = lib.GetDbContext();
                var downloadMangas = libDbContext.MangaDownloadRecords.Find(p => p.Library.CrawlerAgent.Id == agentCrawler.Id);
                var downloadChapters = libDbContext.ChapterDownloadRecords.Find(p => p.CrawlerAgent.Id == agentCrawler.Id);

                foreach (var jobId in downloadMangas.Select(p => p.BackgroundJobId).Union(downloadChapters.Select(p => p.BackgroundJobId)))
                {
                    jobClient.Delete(jobId);
                }

                libDbContext.MangaDownloadRecords.DeleteMany(p => p.Library.CrawlerAgent.Id == agentCrawler.Id);
                libDbContext.ChapterDownloadRecords.DeleteMany(p => p.CrawlerAgent.Id == agentCrawler.Id);
            }

            dbContext.CrawlerAgents.Delete(Id);

            var crawlerAgents = dbContext.CrawlerAgents.FindAll().ToList();

            notificationService.PushSuccessAsync(I18n.CrawlerAgentRemovedSuccessfully, cancellationToken);

            return Partial("_CrawlerAgentList", crawlerAgents);
        }
    }

}
