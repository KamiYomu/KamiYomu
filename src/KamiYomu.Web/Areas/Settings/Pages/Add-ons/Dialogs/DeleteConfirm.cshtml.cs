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

        public IActionResult OnPost()
        {
            dbContext.NugetSources.Delete(Id);

            var nugetSources = dbContext.NugetSources.FindAll().ToList();

            notificationService.PushSuccessAsync("Source removed successfully");

            return Partial("_PackageList", nugetSources);
        }
    }
}
