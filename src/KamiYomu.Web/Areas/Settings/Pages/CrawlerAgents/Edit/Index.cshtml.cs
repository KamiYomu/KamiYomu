using KamiYomu.Web.Areas.Settings.Pages.Shared;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Edit
{
    public class IndexModel(DbContext dbContext, 
                           CacheContext cacheContext, 
                           INotificationService notificationService) : PageModel
    {
        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();
        public IActionResult OnGet(Guid id)
        {
            var crawlerAgent = dbContext.CrawlerAgents.FindById(id);

            if (crawlerAgent == null) return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Index");

            Input = new InputModel()
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
            return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit/Index", new { agentCrawler.Id });
        }
    }

    public class InputModel
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
