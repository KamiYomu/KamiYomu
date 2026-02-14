using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.ViewComponents;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs;

public class DeleteConfirmModel(DbContext dbContext, INotificationService notificationService) : PageModel
{
    [BindProperty]
    public Guid Id { get; set; }

    public NugetSource? NugetSource { get; set; }

    public IActionResult OnGet(Guid id)
    {
        NugetSource = dbContext.NugetSources.FindById(id);
        return NugetSource == null ? NotFound() : Page();
    }

    public IActionResult OnPost(CancellationToken cancellationToken)
    {
        _ = dbContext.NugetSources.Delete(Id);

        _ = notificationService.PushSuccessAsync(I18n.SourceRemovedSuccessfully, cancellationToken);

        SearchBarViewModel viewModel = new()
        {
            Sources = dbContext.NugetSources.FindAll(),
            IncludePrerelease = false
        };

        return ViewComponent("SearchBar", viewModel);
    }
}
