using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.CommunityCrawlers
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly DbContext _dbContext;
        private readonly INugetService _nugetService;

        [BindProperty]
        public string Search { get; set; } = "";

        [BindProperty]
        public Guid SourceId { get; set; } = Guid.Empty;

        public IEnumerable<NugetSource> Sources { get; set; } = [];

        public IEnumerable<NugetPackageInfo> Packages { get; set; } = [];

        public IndexModel(ILogger<IndexModel> logger, DbContext dbContext, INugetService nugetService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _nugetService = nugetService;
        }

        public void OnGet()
        {
            Sources = _dbContext.NugetSources.FindAll();
        }

        public async Task<IActionResult> OnPostSearchAsync()
        {
            try
            {
                Packages = await _nugetService.SearchPackagesAsync(Search, SourceId);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error on search packages");
                Packages = [];
            }

            return Partial("_PackageList", Packages);
        }

    }

}
