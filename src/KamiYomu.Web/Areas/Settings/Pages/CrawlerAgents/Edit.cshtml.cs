using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Eventing.Reader;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents
{
    public class EditModel(DbContext dbContext, 
                           CacheContext cacheContext, 
                           INotificationService notificationService) : PageModel
    {
        [BindProperty]
        public CrawlerAgentEditInputModel Input { get; set; } = new CrawlerAgentEditInputModel();
        public IActionResult OnGet(Guid id)
        {
            var crawlerAgent = dbContext.CrawlerAgents.FindById(id);

            if (crawlerAgent == null) return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Index");

            Input = new CrawlerAgentEditInputModel()
            {
                Id = id,
                DisplayName = crawlerAgent.DisplayName,
                ReadOnlyMetadata = crawlerAgent.GetAssemblyMetadata(),
                CrawlerInputsViewModel = new CrawlerInputsViewModel
                {
                    CrawlerInputs = crawlerAgent.GetCrawlerInputs(),
                    AgentMetadata = CrawlerInputsViewModel.GetAgentMetadataValues(crawlerAgent.AgentMetadata)
                }
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            var agentCrawler = dbContext.CrawlerAgents.FindById(Input.Id);
            agentCrawler.Update(Input.DisplayName, Input.CrawlerInputsViewModel.GetAgentMetadataValues(), Input.ReadOnlyMetadata);
            dbContext.CrawlerAgents.Update(agentCrawler);
            cacheContext.EmptyAgentKeys(agentCrawler.Id);
            await notificationService.PushSuccessAsync(I18n.CrawlerAgentSavedSuccessfully, cancellationToken);
            return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit", new { agentCrawler.Id });
        }
    }


    public class CrawlerAgentEditInputModel
    {
        [BindProperty]
        public Guid? Id { get; set; }
 
        [BindProperty]
        [Required]
        public string? DisplayName { get; set; }

        [BindProperty]
        public CrawlerInputsViewModel CrawlerInputsViewModel { get; set; } = new();

        [BindProperty]
        public Dictionary<string, string> ReadOnlyMetadata { get; set; } = [];

    }
}
