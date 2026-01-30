using System.ComponentModel.DataAnnotations;

using KamiYomu.CrawlerAgents.Core;
using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.Areas.Settings.Pages.Shared;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Edit;

public class IndexModel(DbContext dbContext,
                       CacheContext cacheContext,
                       INotificationService notificationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }
    public IActionResult OnGet()
    {
        FetchData();

        return Input == null ? PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Index") : Page();
    }

    private void FetchData()
    {
        CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(Id);

        Input = new InputModel()
        {
            Id = crawlerAgent.Id,
            DisplayName = crawlerAgent.DisplayName,
            ReadOnlyMetadata = crawlerAgent.GetAssemblyMetadata(),
            CrawlerInputsViewModel = new CrawlerInputsViewModel
            {
                CrawlerInputs = crawlerAgent.GetCrawlerInputs(),
                AgentMetadata = CrawlerInputsViewModel.GetAgentMetadataValues(crawlerAgent.AgentMetadata)
            }
        };
    }

    public IActionResult OnPost()
    {
        CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(Input.Id);
        Dictionary<string, object> metadata = Input.CrawlerInputsViewModel.GetAgentMetadataValues();
        IEnumerable<AbstractInputAttribute> crawlerInputs = crawlerAgent.GetCrawlerInputs();

        foreach (AbstractInputAttribute crawlerInput in crawlerInputs)
        {
            if (crawlerInput.Required)
            {
                if ((metadata.TryGetValue(crawlerInput.Name, out object? valueObj)
                   && valueObj is null)
                   || (valueObj is string valueStr && string.IsNullOrWhiteSpace(valueStr)))
                {
                    ModelState.AddModelError($"AgentMetadata[{crawlerInput.Name}]", I18n.ThisValueIsRequired);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            notificationService.EnqueueErrorForNextPage(I18n.PleaseCorrectHighlightedField);
            Id = Input.Id;
            FetchData();
            return Page();
        }

        crawlerAgent.Update(Input.DisplayName, metadata, Input.ReadOnlyMetadata);

        _ = dbContext.CrawlerAgents.Update(crawlerAgent);

        cacheContext.EmptyAgentKeys(crawlerAgent.Id);

        notificationService.EnqueueSuccessForNextPage(I18n.CrawlerAgentSavedSuccessfully);

        Id = Input.Id;
        Input = new InputModel()
        {
            Id = Input.Id,
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
