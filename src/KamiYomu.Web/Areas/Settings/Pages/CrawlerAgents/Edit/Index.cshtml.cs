using System.ComponentModel.DataAnnotations;

using KamiYomu.CrawlerAgents.Core.Inputs;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Pages.Shared;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.AppServices.Interfaces;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents.Edit;

public class IndexModel(DbContext dbContext,
                       CacheContext cacheContext,
                       IOptions<CloudflareSolverOptions> flareSolverrOptions,
                       ICrawlerAgentAppService crawlerAgentAppService,
                       INotificationService notificationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    [BindProperty]
    public bool RequiresConfirmation { get; set; }
    [BindProperty]
    public bool ShowConfirmationDialog { get; set; }
    [BindProperty]
    public int LibrariesUsingThisCrawlerAgent { get; set; }
    [BindProperty]
    public bool UpgradeAffectedLibraries { get; set; }
    public IActionResult OnGet()
    {
        FetchData();

        return Input == null ? PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Index") : Page();
    }

    private void FetchData()
    {
        // --- Load crawler agent safely ---
        var crawlerAgent = dbContext.CrawlerAgents.FindById(Id);
        if (crawlerAgent is null)
            throw new InvalidOperationException($"CrawlerAgent '{Id}' not found.");

        // --- Build crawler inputs (with FlareSolverr if enabled) ---
        var crawlerInputs = crawlerAgent.GetCrawlerInputs() ?? Enumerable.Empty<AbstractInputAttribute>();

        if (flareSolverrOptions.Value.Enabled)
        {
            crawlerInputs = crawlerInputs.Append(
                new CrawlerTextAttribute(
                    CrawlerAgentMetadata.Fields.FlareSolverrUrl,
                    I18n.FlareSolverrUrl,
                    required: true,
                    flareSolverrOptions.Value.Uri?.ToString(),
                    order: 902
                )
            );
        }

        // --- Count libraries using this agent ---
        LibrariesUsingThisCrawlerAgent = dbContext.Libraries
            .Query()
            .Where(p => p.CrawlerAgent.Id == Id).Count();

        RequiresConfirmation = LibrariesUsingThisCrawlerAgent > 0;

        // --- Initialize Input model only if missing ---
        if (Input is null)
        {
            Input = new InputModel
            {
                Id = crawlerAgent.Id,
                DisplayName = crawlerAgent.DisplayName
            };
        }

        // --- Populate ReadOnlyMetadata only if missing ---
        Input.ReadOnlyMetadata ??= crawlerAgent.GetAssemblyMetadata();

        // --- Ensure CrawlerInputsViewModel exists ---
        if (Input.CrawlerInputsViewModel is null)
        {
            Input.CrawlerInputsViewModel = new CrawlerInputsViewModel();
        }

        // --- Populate AgentMetadata only if missing or empty ---
        if (Input.CrawlerInputsViewModel.AgentMetadata == null ||
            !Input.CrawlerInputsViewModel.AgentMetadata.Any())
        {
            Input.CrawlerInputsViewModel.AgentMetadata =
                CrawlerInputsViewModel.GetAgentMetadataValues(crawlerAgent.AgentMetadata);
        }

        // --- Always set crawler inputs (safe to overwrite) ---
        Input.CrawlerInputsViewModel.CrawlerInputs = crawlerInputs;
    }


    public IActionResult OnPostCancel()
    {
        ShowConfirmationDialog = false;

        // Reload the page with the form visible and no dialog
        Id = Input.Id;
        FetchData();

        return Page();
    }


    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        // Always validate first
        if (!ModelState.IsValid)
        {
            notificationService.EnqueueErrorForNextPage(I18n.PleaseCorrectHighlightedField);
            Id = Input.Id;
            FetchData();
            return Page();
        }

        // Check your backend condition
        if (RequiresConfirmation)
        {
            // Do NOT update yet — show confirmation dialog
            ShowConfirmationDialog = true;

            Id = Input.Id;
            FetchData();
            return Page();
        }

        // No confirmation needed → perform update immediately
        return await PerformCrawlerAgentUpdateAsync(cancellationToken);
    }

    public Task<IActionResult> OnPostConfirmUpgradeAsync(CancellationToken cancellationToken)
    {
        return PerformCrawlerAgentUpdateAsync(cancellationToken);
    }

    private async Task<IActionResult> PerformCrawlerAgentUpdateAsync(CancellationToken cancellationToken)
    {
        CrawlerAgent crawlerAgent = dbContext.CrawlerAgents.FindById(Input.Id);

        Dictionary<string, object> metadata = Input.CrawlerInputsViewModel.GetAgentMetadataValues();
        IEnumerable<AbstractInputAttribute> crawlerInputs = crawlerAgent.GetCrawlerInputs();

        if (flareSolverrOptions.Value.Enabled)
        {
            crawlerInputs = crawlerInputs.Append(
                new CrawlerTextAttribute(
                    CrawlerAgentMetadata.Fields.FlareSolverrUrl,
                    I18n.FlareSolverrUrl,
                    true,
                    flareSolverrOptions.Value.Uri?.ToString(),
                    902
                )
            );
        }

        // Validate required metadata fields
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

        // Perform update
        crawlerAgent.Update(Input.DisplayName, metadata, Input.ReadOnlyMetadata);
        _ = dbContext.CrawlerAgents.Update(crawlerAgent);

        if (UpgradeAffectedLibraries)
        {
            _ = await crawlerAgentAppService.RefreshCollectionAsync(crawlerAgent, cancellationToken);
        }
        cacheContext.EmptyAgentKeys(crawlerAgent.Id);

        notificationService.EnqueueSuccessForNextPage(I18n.CrawlerAgentSavedSuccessfully);

        // Rebuild Input model so the page reloads with updated values
        Id = Input.Id;

        Input = new InputModel()
        {
            Id = Input.Id,
            DisplayName = crawlerAgent.DisplayName,
            ReadOnlyMetadata = crawlerAgent.GetAssemblyMetadata(),
            CrawlerInputsViewModel = new CrawlerInputsViewModel
            {
                CrawlerInputs = crawlerInputs,
                AgentMetadata = CrawlerInputsViewModel.GetAgentMetadataValues(crawlerAgent.AgentMetadata)
            }
        };

        return RedirectToPage("/CrawlerAgents/Edit/Index", new { Id = Input.Id });
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
