using KamiYomu.Web.Areas.Settings.Pages.CommunityCrawlers;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using static KamiYomu.Web.Settings;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs
{
    public class DeleteConfirmModel(DbContext dbContext) : PageModel
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
            return Partial("_PackageList", nugetSources);
        }
    }
}
