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

        public async Task<IActionResult> OnPostSearchAsync(CancellationToken cancellationToken)
        {
            try
            {
                Packages = await nugetService.SearchPackagesAsync(SearchBarViewModel.Search, SearchBarViewModel.IncludePrerelease, SearchBarViewModel.SourceId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error on search packages");
                await notificationService.PushErrorAsync("Failed to search packages from the source.", cancellationToken);
                Packages = [];
            }

            return Partial("_PackageList", new PackageListViewModel
            {
                SourceId = SearchBarViewModel.SourceId,
                Packages = Packages
            });
        }

        public async Task<IActionResult> OnPostInstallAsync(Guid sourceId, string packageId, string packageVersion, CancellationToken cancellationToken)
        {
            try
            {
                var streams = await nugetService.OnGetDownloadAsync(sourceId, packageId, packageVersion, cancellationToken);

                var tempUploadId = Guid.NewGuid();
                var downloadDir = Path.Combine(Path.GetTempPath(), tempUploadId.ToString());
                Directory.CreateDirectory(downloadDir);

                var packageFileName = $"{packageId}.{packageVersion}.nupkg";
                var crawlerAgentDir = CrawlerAgent.GetAgentDir(packageFileName);

                if(Directory.Exists(crawlerAgentDir))
                    Directory.Delete(crawlerAgentDir, recursive: true); 

                Directory.CreateDirectory(crawlerAgentDir);

                var savedPaths = new List<string>();
                string mainPackagePath = null;

                for (int i = 0; i < streams.Length; i++)
                {
                    var fileName = i == 0
                        ? packageFileName
                        : $"dependency_{i}.nupkg";

                    var filePath = Path.Combine(downloadDir, fileName);
                    using var fileStream = System.IO.File.Create(filePath);
                    await streams[i].CopyToAsync(fileStream, cancellationToken);

                    savedPaths.Add(filePath);
                    if (i == 0) mainPackagePath = filePath;
                }

                // Extract main package first into a dedicated subfolder
                ZipFile.ExtractToDirectory(mainPackagePath, crawlerAgentDir, overwriteFiles: true);

                // Scan only the main package's extracted folder for the DLL
                var dllPath = Directory.EnumerateFiles(crawlerAgentDir, "*.dll", SearchOption.AllDirectories).FirstOrDefault();
                if (dllPath == null)
                    throw new FileNotFoundException("Main package DLL not found.");

                // Extract dependencies into the same root directory
                foreach (var path in savedPaths.Skip(1))
                {
                    ZipFile.ExtractToDirectory(path, crawlerAgentDir, overwriteFiles: true);
                }

                var assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
                var metadata = CrawlerAgent.GetAssemblyMetadata(assembly);
                var displayName = CrawlerAgent.GetCrawlerDisplayName(assembly);

                var crawlerAgent = new CrawlerAgent(dllPath, displayName, []);
                dbContext.CrawlerAgents.Insert(crawlerAgent);

                dbContext.CrawlerAgentFileStorage.Delete(tempUploadId);

                return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit", new { crawlerAgent.Id });
            }
            catch (Exception ex)
            {
                await notificationService.PushErrorAsync("Package is invalid.", cancellationToken);
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
