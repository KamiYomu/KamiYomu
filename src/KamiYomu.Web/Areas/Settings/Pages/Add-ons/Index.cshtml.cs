using KamiYomu.Web.Areas.Settings.Pages.CrawlerAgents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Polly;
using System.IO.Compression;

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

        public PackageListViewModel PackageListViewModel { get; set; } = new();

        public IndexModel(ILogger<IndexModel> logger, DbContext dbContext, INugetService nugetService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _nugetService = nugetService;
        }

        public void OnGet()
        {
            Sources = _dbContext.NugetSources.FindAll();
            PackageListViewModel = new PackageListViewModel
            {
                SourceId = SourceId,
                Packages = Packages
            };
        }

        public async Task<IActionResult> OnPostSearchAsync()
        {
            try
            {
                Packages = await _nugetService.SearchPackagesAsync(Search, SourceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error on search packages");
                Packages = [];
            }

            return Partial("_PackageList", new PackageListViewModel
            {
                SourceId = SourceId,
                Packages = Packages
            });
        }


        public async Task<IActionResult> OnPostInstallAsync(Guid sourceId, string packageId, string packageVersion)
        {
            try
            {
                var stream = await _nugetService.OnGetDownloadAsync(sourceId, packageId, packageVersion);

                var tempUploadId = Guid.NewGuid();
                var tempFileName = $"{packageId}.{packageVersion}.nupkg";
                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

                _dbContext.CrawlerAgentFileStorage.Upload(tempUploadId, tempFileName, stream);

                var fileStorage = _dbContext.CrawlerAgentFileStorage.FindById(tempUploadId);
                fileStorage.SaveAs(tempFilePath);

                var crawlerAgentDir = CrawlerAgent.GetAgentDir(tempFileName);

                ZipFile.ExtractToDirectory(tempFilePath, crawlerAgentDir, true);

                var dllPath = Directory.EnumerateFiles(crawlerAgentDir, searchPattern: "*.dll", SearchOption.AllDirectories).FirstOrDefault();

                var assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
                var metadata = CrawlerAgent.GetAssemblyMetadata(assembly);
                var displayName = CrawlerAgent.GetCrawlerDisplayName(assembly);

                // Register agent
                var crawlerAgent = new CrawlerAgent(dllPath, displayName, new Dictionary<string, object>());
                _dbContext.CrawlerAgents.Insert(crawlerAgent);

                _dbContext.CrawlerAgentFileStorage.Delete(tempUploadId);

                return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit", new
                {
                    crawlerAgent.Id
                });

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Package is invalid.");
            }

            ModelState.Remove("Search");

            Sources = _dbContext.NugetSources.FindAll();
            PackageListViewModel = new PackageListViewModel
            {
                SourceId = SourceId,
                Packages = Packages
            };

            return Page();
        }

    }

    public class PackageListViewModel
    {
        public Guid SourceId { get; set; }
        public IEnumerable<NugetPackageInfo> Packages { get; set; } = Enumerable.Empty<NugetPackageInfo>();
    }


}
