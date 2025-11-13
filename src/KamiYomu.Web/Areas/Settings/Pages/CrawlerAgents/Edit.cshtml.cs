using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

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
                AgentMetadata = CrawlerAgentEditInputModel.GetAgentMetadataValues(crawlerAgent.AgentMetadata),
                CrawlerTexts = crawlerAgent.GetCrawlerTexts(),
                CrawlerPasswords = crawlerAgent.GetCrawlerPasswords(),
                CrawlerCheckBoxs = crawlerAgent.GetCrawlerCheckBoxs(),
                CrawlerSelects = crawlerAgent.GetCrawlerSelects(),
                ReadOnlyMetadata = crawlerAgent.GetAssemblyMetadata()
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            var agentCrawler = dbContext.CrawlerAgents.FindById(Input.Id);
            agentCrawler.Update(Input.DisplayName, Input.GetAgentMetadataValues(), Input.ReadOnlyMetadata);
            dbContext.CrawlerAgents.Update(agentCrawler);
            cacheContext.EmptyAgentKeys(agentCrawler.Id);
            await notificationService.PushSuccessAsync(I18n.CrawlerAgentSavedSuccessfully);
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
        public IEnumerable<CrawlerPasswordAttribute> CrawlerPasswords { get; set; } = [];
        [BindProperty]
        public IEnumerable<CrawlerTextAttribute> CrawlerTexts { get; set; } = [];
        [BindProperty]
        public IEnumerable<CrawlerSelectAttribute> CrawlerSelects { get; set; } = [];
        [BindProperty]
        public IEnumerable<CrawlerCheckBoxAttribute> CrawlerCheckBoxs { get; set; } = [];
        [BindProperty]
        public Dictionary<string, string> AgentMetadata { get; set; } = [];
        [BindProperty]
        public Dictionary<string, string> ReadOnlyMetadata { get; set; } = [];

        public Dictionary<string, object> GetAgentMetadataValues()
        {
            Dictionary<string, object> metadata = [];
            foreach (var item in AgentMetadata)
            {
                if (bool.TryParse(item.Value, out var boolValue))
                {
                    metadata[item.Key] = boolValue;
                }
                else
                {
                    metadata[item.Key] = item.Value;
                }
            }
            return metadata;
        }

        public static Dictionary<string, string> GetAgentMetadataValues(Dictionary<string, object> values)
        {
            Dictionary<string, string> metadata = [];
            foreach (var item in values)
            {
                metadata[item.Key] = item.Value?.ToString();
            }
            return metadata;
        }
    }
}
