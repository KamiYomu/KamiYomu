using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.Dialogs
{
    public class ConfirmAddNugetOrgModel(DbContext dbContext, INotificationService notificationService) : PageModel
    {

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            dbContext.NugetSources.Insert(new NugetSource("NuGet.org", new Uri(Defaults.NugetFeeds.NugetFeedUrl), null!, null!));

            var nugetSources = dbContext.NugetSources.FindAll().ToList();

            notificationService.PushSuccessAsync("Source added successfully");

            var viewModel = new SearchBarViewModel
            {
                Sources = dbContext.NugetSources.FindAll(),
                IncludePrerelease = false
            };

            return Partial("_SearchBar", viewModel);
        }
    }
}
