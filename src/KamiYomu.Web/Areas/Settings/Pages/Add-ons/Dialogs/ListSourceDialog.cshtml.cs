using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs
{
    public class ListSourceDialogModel(DbContext dbContext) : PageModel
    {
        public List<NugetSource> Sources { get; set; } = new();

        public async Task OnGetAsync()
        {
            Sources = dbContext.NugetSources.FindAll().ToList();
        }

       

    }
}
