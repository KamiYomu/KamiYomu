using KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO.Compression;

namespace KamiYomu.Web.Areas.Settings.Pages.CommunityCrawlers
{
    public class IndexModel(ILogger<IndexModel> logger,
                            DbContext dbContext,
                            INugetService nugetService,
                            INotificationService notificationService) : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public SearchBarViewModel SearchBarViewModel { get; set; } = new();

        public IEnumerable<NugetPackageInfo> Packages { get; set; } = [];

        public PackageListViewModel PackageListViewModel { get; set; } = new();

        public bool IsNugetAdded { get; set; } = false;

        public void OnGet()
        {

            PackageListViewModel = new PackageListViewModel
            {
                SourceId = SearchBarViewModel.SourceId,
                Packages = Packages
            };
            SearchBarViewModel = new SearchBarViewModel
            {
                SourceId = SearchBarViewModel.SourceId,
                Sources = dbContext.NugetSources.FindAll()
            };

            IsNugetAdded = SearchBarViewModel.Sources.Any(p => p.Url.ToString().StartsWith(AppOptions.Defaults.NugetFeeds.NugetFeedUrl, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<IActionResult> OnPostSearchAsync()
        {
            try
            {
                Packages = await nugetService.SearchPackagesAsync(SearchBarViewModel.Search, SearchBarViewModel.IncludePrerelease, SearchBarViewModel.SourceId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error on search packages");
                await notificationService.PushErrorAsync("Failed to search packages from the source.");
                Packages = [];
            }

            return Partial("_PackageList", new PackageListViewModel
            {
                SourceId = SearchBarViewModel.SourceId,
                Packages = Packages
            });
        }


        public async Task<IActionResult> OnPostInstallAsync(Guid sourceId, string packageId, string packageVersion)
        {
            try
            {
                var stream = await nugetService.OnGetDownloadAsync(sourceId, packageId, packageVersion);

                var tempUploadId = Guid.NewGuid();
                var tempFileName = $"{packageId}.{packageVersion}.nupkg";
                var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

                dbContext.CrawlerAgentFileStorage.Upload(tempUploadId, tempFileName, stream);

                var fileStorage = dbContext.CrawlerAgentFileStorage.FindById(tempUploadId);
                fileStorage.SaveAs(tempFilePath);

                var crawlerAgentDir = CrawlerAgent.GetAgentDir(tempFileName);

                ZipFile.ExtractToDirectory(tempFilePath, crawlerAgentDir, true);

                var dllPath = Directory.EnumerateFiles(crawlerAgentDir, searchPattern: "*.dll", SearchOption.AllDirectories).FirstOrDefault();

                var assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
                var metadata = CrawlerAgent.GetAssemblyMetadata(assembly);
                var displayName = CrawlerAgent.GetCrawlerDisplayName(assembly);

                // Register agent
                var crawlerAgent = new CrawlerAgent(dllPath, displayName, new Dictionary<string, object>());
                dbContext.CrawlerAgents.Insert(crawlerAgent);

                dbContext.CrawlerAgentFileStorage.Delete(tempUploadId);

                return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit", new
                {
                    crawlerAgent.Id
                });

            }
            catch (Exception ex)
            {
                await notificationService.PushErrorAsync("Package is invalid.");
            }

            ModelState.Remove("Search");

            SearchBarViewModel.Sources = dbContext.NugetSources.FindAll();
            PackageListViewModel = new PackageListViewModel
            {
                SourceId = SearchBarViewModel.SourceId,
                Packages = Packages
            };

            return Page();
        }

    }

}
