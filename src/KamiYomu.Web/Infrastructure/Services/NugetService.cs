using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;
using static SQLite.SQLite3;

namespace KamiYomu.Web.Infrastructure.Services
{
    public class NugetService : INugetService
    {
        private readonly HttpClient _httpClient;
        private readonly DbContext _dbContext;

        public NugetService(IHttpClientFactory httpClientFactory, DbContext dbContext)
        {
            _httpClient = httpClientFactory.CreateClient();
            _dbContext = dbContext;
        }

        public async Task<NugetPackageInfo?> GetPackageMetadataAsync(string packageName, Guid sourceId)
        {
            var source = _dbContext.NugetSources.FindById(sourceId);

            var indexUrl = source.Url.ToString();
            var indexJson = await _httpClient.GetStringAsync(indexUrl);
            var index = JsonNode.Parse(indexJson);

            var metadataUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "RegistrationsBaseUrl")?["@id"]?.ToString();

            if (string.IsNullOrEmpty(metadataUrl))
                throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");

            var packageUrl = $"{metadataUrl.TrimEnd('/')}/{packageName.ToLowerInvariant()}/index.json";
            var packageJson = await _httpClient.GetStringAsync(packageUrl);
            var packageNode = JsonNode.Parse(packageJson)?["items"]?.AsArray()?[0]?["items"]?.AsArray()?[0]?["catalogEntry"];

            if (packageNode == null)
                return null;

            var packageTypes = packageNode["packageTypes"]?.AsArray();
            var isCrawlerAgent = packageTypes?.Any(pt => pt?["name"]?.ToString()?.Equals(Settings.Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase) == true) ?? false;

            if (!isCrawlerAgent)
                return null;

            return new NugetPackageInfo
            {
                Id = packageNode["id"]?.ToString(),
                Version = packageNode["version"]?.ToString(),
                IconUrl = Uri.TryCreate(packageNode["iconUrl"]?.ToString(), UriKind.Absolute, out var icon) ? icon : null,
                Description = packageNode["description"]?.ToString(),
                Authors = packageNode["authors"]?.AsArray()?.Select(p => p?.ToString()).Where(p => p != null).ToArray() ?? Array.Empty<string>(),
                RepositoryUrl = Uri.TryCreate(packageNode["projectUrl"]?.ToString(), UriKind.Absolute, out var repo) ? repo : null,
                TotalDownloads = int.TryParse(packageNode?["totalDownloads"]?.ToString(), out var totalDownload) ? totalDownload : 0,
            };
        }

        public async Task<IEnumerable<NugetPackageInfo>> SearchPackagesAsync(string query, Guid sourceId)
        {
            var indexUrl = $"{sourceUrl.TrimEnd('/')}/v3/index.json";
            var indexJson = await _httpClient.GetStringAsync(indexUrl);
            var index = JsonNode.Parse(indexJson);

            var searchUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "SearchQueryService")?["@id"]?.ToString();

            if (string.IsNullOrEmpty(searchUrl))
                throw new InvalidOperationException("SearchQueryService not found in index.json");

            var searchQueryUrl = $"{searchUrl}?q={Uri.EscapeDataString(query)}&prerelease=true&take=20";
            var searchJson = await _httpClient.GetStringAsync(searchQueryUrl);
            var searchResults = JsonNode.Parse(searchJson)?["data"]?.AsArray();

            var packages = new List<NugetPackageInfo>();

            if (searchResults != null)
            {
                foreach (var result in searchResults)
                {
                    var packageTypes = result?["packageTypes"]?.AsArray();
                    var isCrawlerAgent = packageTypes?.Any(pt =>
                        pt?["name"]?.ToString()?.Equals(Settings.Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase) == true
                    ) ?? false;

                    if (!isCrawlerAgent)
                        continue;

                    packages.Add(new NugetPackageInfo
                    {
                        Id = result?["id"]?.ToString(),
                        Version = result?["version"]?.ToString(),
                        IconUrl = Uri.TryCreate(result?["iconUrl"]?.ToString(), UriKind.Absolute, out var icon) ? icon : null,
                        Description = result?["description"]?.ToString(),
                        Authors = result?["authors"]?.AsArray()?.Select(p => p?.ToString()).Where(p => p != null).ToArray() ?? Array.Empty<string>(),
                        RepositoryUrl = Uri.TryCreate(result?["projectUrl"]?.ToString(), UriKind.Absolute, out var repo) ? repo : null,
                        TotalDownloads = int.TryParse(result?["totalDownloads"]?.ToString(), out var totalDownload) ? totalDownload : 0
                    });
                }
            }

            return packages;
        }
    }
}
