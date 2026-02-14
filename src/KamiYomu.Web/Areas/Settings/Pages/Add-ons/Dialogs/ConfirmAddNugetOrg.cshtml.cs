using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.ViewComponents;
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

        _ = notificationService.PushSuccessAsync("Source added successfully", cancellationToken);

        SearchBarViewModel viewModel = new()
        {
            SourceId = source.Id,
            Sources = dbContext.NugetSources.FindAll(),
            IncludePrerelease = false
        };

        return ViewComponent("SearchBar", viewModel);
    }
}
