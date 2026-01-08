using System.IO.Compression;

using KamiYomu.Web.Areas.Settings.Pages.Add_ons.ViewModels;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KamiYomu.Web.Areas.Settings.Pages.CommunityCrawlers;

public class IndexModel(ILogger<IndexModel> logger,
                        DbContext dbContext,
                        INugetService nugetService,
                        INotificationService notificationService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public SearchBarViewModel SearchBarViewModel { get; set; } = new();

    public PackageListViewModel PackageListViewModel { get; set; } = new();

    public bool IsNugetAdded { get; set; } = false;

    public void OnGet()
    {

        PackageListViewModel = new PackageListViewModel
        {
            SourceId = SearchBarViewModel.SourceId,
            PackageItems = []
        };
        SearchBarViewModel = new SearchBarViewModel
        {
            SourceId = SearchBarViewModel.SourceId,
            Sources = dbContext.NugetSources.FindAll()
        };

        IsNugetAdded = SearchBarViewModel.Sources.Any(p => p.Url.ToString().StartsWith(AppOptions.Defaults.NugetFeeds.NugetFeedUrl, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IActionResult> OnGetPackageItemAsync(Guid sourceId, string packageId, string version, CancellationToken cancellationToken)
    {
        NugetPackageInfo? package = await nugetService.GetPackageMetadataAsync(sourceId, packageId, version, cancellationToken);

        IEnumerable<NugetPackageInfo> packageVersions = await nugetService.GetAllPackageVersionsAsync(sourceId, packageId, cancellationToken);

        return Partial("_PackageItem", new NugetPackageGroupedViewModel
        {
            Authors = package.Authors ?? [],
            Description = package.Description,
            Id = package.Id!,
            IconUrl = package.IconUrl,
            LicenseUrl = package.LicenseUrl,
            RepositoryUrl = package.RepositoryUrl,
            SourceId = sourceId,
            VersionSelected = package,
            Versions = packageVersions.OrderByDescending(p => p.Version).Select(p => p.Version).Where(v => v != null)!,
            Tags = package.Tags ?? [],
            TotalDownloads = package.TotalDownloads ?? 0,
            DependenciesByVersion = packageVersions
                .Where(p => !string.IsNullOrEmpty(p.Version))
                .ToDictionary(
                    p => p.Version!,
                    p => p.Dependencies ?? []
                )
        });
    }

    public async Task<IActionResult> OnPostSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            string searchTerm = string.IsNullOrWhiteSpace(SearchBarViewModel.Search) ? "KamiYomu" : SearchBarViewModel.Search;
            IEnumerable<NugetPackageInfo> packages = await nugetService.SearchPackagesAsync(SearchBarViewModel.SourceId, searchTerm, SearchBarViewModel.IncludePrerelease, cancellationToken);
            packages = packages.OrderBy(p => p.Id).ThenByDescending(p => p.Version);
            PackageListViewModel = new PackageListViewModel
            {
                SourceId = SearchBarViewModel.SourceId,
                PackageItems = packages.GroupBy(p => p.Id)
                                       .Select(g => new NugetPackageGroupedViewModel
                                       {
                                           Id = g.Key!,
                                           SourceId = SearchBarViewModel.SourceId,
                                           IconUrl = g.FirstOrDefault()?.IconUrl,
                                           Description = g.FirstOrDefault()?.Description,
                                           Authors = g.FirstOrDefault()?.Authors ?? [],
                                           Tags = g.FirstOrDefault()?.Tags ?? [],
                                           TotalDownloads = g.Sum(x => x.TotalDownloads ?? 0),
                                           LicenseUrl = g.FirstOrDefault()?.LicenseUrl,
                                           RepositoryUrl = g.FirstOrDefault()?.RepositoryUrl,
                                           VersionSelected = g.FirstOrDefault() ?? null,
                                           Versions = g.Select(x => x.Version!).ToList(),
                                           DependenciesByVersion = g
                                               .Where(x => x.Version != null)
                                               .ToDictionary(
                                                   x => x.Version!,
                                                   x => x.Dependencies ?? []
                                               )
                                       })
            };

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error on search packages");
            notificationService.EnqueueErrorForNextPage(I18n.FailedToSearch);
        }

        return Partial("_PackageList", PackageListViewModel);
    }

    public async Task<IActionResult> OnPostInstallAsync(Guid sourceId, string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        try
        {
            Stream[] streams = await nugetService.OnGetDownloadAsync(sourceId, packageId, packageVersion, cancellationToken);

            Guid tempUploadId = Guid.NewGuid();
            string downloadDir = Path.Combine(Path.GetTempPath(), AppOptions.Defaults.Worker.TempDirName, tempUploadId.ToString());
            _ = Directory.CreateDirectory(downloadDir);

            string packageFileName = $"{packageId}.{packageVersion}.nupkg";
            string crawlerAgentDir = CrawlerAgent.GetAgentDir(packageFileName);

            List<string> savedPaths = [];
            string tempPackagePath = null;

            for (int i = 0; i < streams.Length; i++)
            {
                string fileName = i == 0
                    ? packageFileName
                    : $"dependency_{i}.nupkg";

                string filePath = Path.Combine(downloadDir, fileName);
                using FileStream fileStream = System.IO.File.Create(filePath);
                await streams[i].CopyToAsync(fileStream, cancellationToken);

                savedPaths.Add(filePath);
                if (i == 0)
                {
                    tempPackagePath = filePath;
                }
            }

            // Extract main package first into a dedicated subfolder
            ZipFile.ExtractToDirectory(tempPackagePath, crawlerAgentDir, overwriteFiles: true);

            // Scan only the main package's extracted folder for the DLL
            string dllPath = Directory.EnumerateFiles(crawlerAgentDir, "*.dll", SearchOption.AllDirectories).FirstOrDefault(p => p.EndsWith($"{packageId}.dll")) ?? throw new FileNotFoundException("Main package DLL not found.");

            // Extract dependencies into the same root directory
            foreach (string? path in savedPaths.Skip(1))
            {
                ZipFile.ExtractToDirectory(path, crawlerAgentDir, overwriteFiles: true);
            }

            System.Reflection.Assembly assembly = CrawlerAgent.GetIsolatedAssembly(dllPath);
            Dictionary<string, string> metadata = CrawlerAgent.GetAssemblyMetadata(assembly);
            string displayName = CrawlerAgent.GetCrawlerDisplayName(assembly);

            using CrawlerAgent crawlerAgent = new(dllPath, displayName, []);
            _ = dbContext.CrawlerAgents.Insert(crawlerAgent);

            _ = dbContext.CrawlerAgentFileStorage.Delete(tempUploadId);

            notificationService.EnqueueSuccessForNextPage(I18n.NuGetPackageInstalledSuccessfully);

            return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit/Index", new { crawlerAgent.Id });
        }
        catch (Exception ex)
        {

            logger.LogError(ex, "Error on install package {PackageId} {PackageVersion} from source {SourceId}", packageId, packageVersion, sourceId);

            notificationService.EnqueueErrorForNextPage(I18n.NuGetPackageIsInvalid);
        }

        _ = ModelState.Remove("Search");

        SearchBarViewModel.Sources = dbContext.NugetSources.FindAll();
        PackageListViewModel = new PackageListViewModel
        {
            SourceId = SearchBarViewModel.SourceId,
            PackageItems = []
        };

        return Page();
    }
}
