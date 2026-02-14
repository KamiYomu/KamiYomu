using System.IO.Compression;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Areas.Settings.ViewComponents;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Areas.Settings.Pages.CommunityCrawlers;

public class IndexModel(ILogger<IndexModel> logger,
                        IOptions<StartupOptions> startupOptions,
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

        IsNugetAdded = SearchBarViewModel.Sources.Any(p => p.Url.ToString().StartsWith(Defaults.NugetFeeds.NugetFeedUrl, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IActionResult> OnGetPackageItemAsync(Guid sourceId, string packageId, string version, CancellationToken cancellationToken)
    {
        NugetPackageInfo? package = await nugetService.GetPackageMetadataAsync(sourceId, packageId, version, cancellationToken);

        IEnumerable<NugetPackageInfo> packageVersions = await nugetService.GetAllPackageVersionsAsync(sourceId, packageId, cancellationToken);

        return ViewComponent("PackageItem", new NugetPackageGroupedViewModel
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

    public async Task<IActionResult> OnGetSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            UserPreference preferences = dbContext.UserPreferences.Query().FirstOrDefault();
            bool familySafeMode = preferences?.FamilySafeMode ?? true;
            SearchBarViewModel.Search = string.IsNullOrWhiteSpace(SearchBarViewModel.Search) ? startupOptions.Value.DefaultSearchTerm : SearchBarViewModel.Search;
            IEnumerable<NugetPackageInfo> packages = await nugetService.SearchPackagesAsync(SearchBarViewModel.SourceId, SearchBarViewModel.Search, SearchBarViewModel.IncludePrerelease, cancellationToken);
            packages = packages.Where(p => !familySafeMode || !p.IsNsfw()).OrderBy(p => p.Id).ThenByDescending(p => p.Version);
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
                                           Versions = [.. g.Select(x => x.Version!)],
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

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return ViewComponent("PackageList", PackageListViewModel);
        }

        SearchBarViewModel = new SearchBarViewModel
        {
            SourceId = SearchBarViewModel.SourceId,
            Sources = dbContext.NugetSources.FindAll()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostInstallAsync(Guid sourceId, string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        try
        {
            Stream[] streams = await nugetService.OnGetDownloadAsync(sourceId, packageId, packageVersion, cancellationToken);

            Guid tempUploadId = Guid.NewGuid();
            string downloadDir = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, tempUploadId.ToString());
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

            return PageExtensions.RedirectToAreaPage("Settings", "/CrawlerAgents/Edit/Index", new { crawlerAgent.Id, crawlerAgent.DisplayName });
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


    public async Task<IActionResult> OnGetDownloadAsync(
            Guid sourceId,
            string packageId,
            string packageVersion,
            CancellationToken cancellationToken)
    {
        if (sourceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(packageId) ||
            string.IsNullOrWhiteSpace(packageVersion))
        {
            return BadRequest();
        }

        try
        {
            Stream[]? streams = await nugetService.OnGetDownloadAsync(
                sourceId,
                packageId,
                packageVersion,
                cancellationToken);

            if (streams is null || streams.Length == 0 || streams[0] is null)
            {
                logger.LogWarning(
                    "No stream returned for package {PackageId} {PackageVersion} from source {SourceId}",
                    packageId, packageVersion, sourceId);

                notificationService.EnqueueErrorForNextPage(I18n.NuGetPackageIsInvalid);
                return NotFound();
            }

            string packageFileName = $"{packageId}.{packageVersion}.nupkg";

            if (streams[0].CanSeek)
            {
                streams[0].Position = 0;
            }

            return File(
                fileStream: streams[0],
                contentType: "application/octet-stream",
                fileDownloadName: packageFileName);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation(
                "Download cancelled for package {PackageId} {PackageVersion} from source {SourceId}",
                packageId, packageVersion, sourceId);

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error downloading package {PackageId} {PackageVersion} from source {SourceId}",
                packageId, packageVersion, sourceId);

            await notificationService.PushErrorAsync(I18n.NuGetPackageIsInvalid, cancellationToken);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

}
