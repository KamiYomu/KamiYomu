using KamiYomu.Web.Entities.Addons;
using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels
{
    public class SearchBarViewModel
    {
        [BindProperty]
        public string? Search { get; set; } = string.Empty;

        [BindProperty]
        public bool IncludePrerelease { get; set; } = false;

        [BindProperty]
        public Guid SourceId { get; set; } = Guid.Empty;

        public IEnumerable<NugetSource> Sources { get; set; } = [];

    }

}
