using KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs
{
    public class DeleteConfirmModel(DbContext dbContext, INotificationService notificationService) : PageModel
    {
        [BindProperty]
        public Guid Id { get; set; }

        public NugetSource? NugetSource { get; set; }

        public IActionResult OnGet(Guid id)
        {
            NugetSource = dbContext.NugetSources.FindById(id);
            if (NugetSource == null) return NotFound();
            return Page();
        }

        public IActionResult OnPostAsync(CancellationToken cancellationToken)
        {
            dbContext.NugetSources.Delete(Id);

            notificationService.PushSuccessAsync("Source removed successfully", cancellationToken);

            var viewModel = new SearchBarViewModel
            {
                Sources = dbContext.NugetSources.FindAll(),
                IncludePrerelease = false
            };

            return Partial("_SearchBar", viewModel);
        }
    }
}
