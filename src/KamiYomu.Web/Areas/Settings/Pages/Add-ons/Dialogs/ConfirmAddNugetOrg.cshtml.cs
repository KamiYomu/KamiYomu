using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs;

public class ConfirmAddNugetOrgModel(DbContext dbContext, INotificationService notificationService) : PageModel
{

    public void OnGet()
    {
    }

    public IActionResult OnPostAsync(CancellationToken cancellationToken)
    {
        NugetSource source = new("NuGet.org", new Uri(Defaults.NugetFeeds.NugetFeedUrl), null, null);
        _ = dbContext.NugetSources.Insert(source);

        List<NugetSource> nugetSources = [.. dbContext.NugetSources.FindAll()];

        _ = notificationService.PushSuccessAsync("Source added successfully", cancellationToken);

        SearchBarViewModel viewModel = new()
        {
            SourceId = source.Id,
            Sources = dbContext.NugetSources.FindAll(),
            IncludePrerelease = false
        };

        return Partial("_SearchBar", viewModel);
    }
}
