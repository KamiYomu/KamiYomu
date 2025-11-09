using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace KamiYomu.Web.Infrastructure.Services
{
    public class NugetService : INugetService
    {
        private readonly DbContext _dbContext;

        public NugetService(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<NugetPackageInfo?> GetPackageMetadataAsync(string packageName, Guid sourceId)
        {
            var source = _dbContext.NugetSources.FindById(sourceId);
            if (source == null)
                throw new InvalidOperationException("NuGet source not found.");

            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler);

            if (!string.IsNullOrWhiteSpace(source.UserName) && !string.IsNullOrWhiteSpace(source.Password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{source.UserName}:{source.Password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
            else if (!string.IsNullOrWhiteSpace(source.Password)) 
            {
                client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", source.Password);
            }

            var indexUrl = source.Url.ToString();
            var indexJson = await client.GetStringAsync(indexUrl);
            var index = JsonNode.Parse(indexJson);

            var metadataUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "RegistrationsBaseUrl")?["@id"]?.ToString();

            if (string.IsNullOrEmpty(metadataUrl))
                throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");

            var packageUrl = $"{metadataUrl.TrimEnd('/')}/{packageName.ToLowerInvariant()}/index.json";
            var packageJson = await client.GetStringAsync(packageUrl);
            var result = JsonNode.Parse(packageJson)?["items"]?.AsArray()?[0]?["items"]?.AsArray()?[0]?["catalogEntry"];

            if (result == null)
                return null;

            var packageTypes = result?["tags"]?.ToString();
            var isCrawlerAgent = packageTypes?.Contains(Settings.Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase) == true;

            if (!isCrawlerAgent)
                return null;

            NugetPackageInfo packageInfo = ConvertToNuGetPackageInfo(result);
            return packageInfo;
        }

        public async Task<IEnumerable<NugetPackageInfo>> SearchPackagesAsync(string query, Guid sourceId)
        {
            var source = _dbContext.NugetSources.FindById(sourceId);
            if (source == null)
                throw new InvalidOperationException("NuGet source not found.");

            using var requestHandler = new HttpClientHandler();
            using var client = new HttpClient(requestHandler);

            if (!string.IsNullOrWhiteSpace(source.UserName) && !string.IsNullOrWhiteSpace(source.Password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{source.UserName}:{source.Password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
            else if (!string.IsNullOrWhiteSpace(source.Password)) 
            {
                client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", source.Password);
            }

            // Fetch service index
            var indexJson = await client.GetStringAsync(source.Url);
            var index = JsonNode.Parse(indexJson);

            var searchUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "SearchQueryService")?["@id"]?.ToString();

            if (string.IsNullOrEmpty(searchUrl))
                throw new InvalidOperationException("SearchQueryService not found in index.json");

            var searchQueryUrl = $"{searchUrl}?q={Uri.EscapeDataString(query)}&prerelease=true&take=20";
            var searchJson = await client.GetStringAsync(searchQueryUrl);
            var searchResults = JsonNode.Parse(searchJson)?["data"]?.AsArray();

            var packages = new List<NugetPackageInfo>();

            if (searchResults != null)
            {
                foreach (var result in searchResults)
                {
                    var tagsNode = result?["tags"];
                    string[] tags = tagsNode switch
                    {
                        JsonArray array => array
                            .Select(t => t?.ToString())
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToArray(),

                        { } node when !string.IsNullOrWhiteSpace(node.ToString()) =>
                            node.ToString().Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries),

                        _ => Array.Empty<string>()
                    };

                    var isCrawlerAgent = tags.Any(tag =>
                        tag.Equals(Settings.Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase));
                    
                    if (!isCrawlerAgent)
                        continue;

                    NugetPackageInfo packageInfo = ConvertToNuGetPackageInfo(result);
                    packages.Add(packageInfo);
                }
            }

            return packages;
        }

        private static NugetPackageInfo ConvertToNuGetPackageInfo(JsonNode? result)
        {
            var authorsNode = result?["authors"];

            var authors = authorsNode is JsonArray array
                ? array.Select(p => p?.ToString()).Where(p => !string.IsNullOrEmpty(p)).ToArray()
                : authorsNode?.ToString() is string singleAuthor && !string.IsNullOrEmpty(singleAuthor)
                    ? [singleAuthor]
                    : Array.Empty<string>();

            var packageInfo = new NugetPackageInfo
            {
                Id = result?["id"]?.ToString(),
                Version = result?["version"]?.ToString(),
                IconUrl = Uri.TryCreate(result?["iconUrl"]?.ToString(), UriKind.Absolute, out var icon) ? icon : null,
                Description = result?["description"]?.ToString(),
                Authors = authors,
                RepositoryUrl = Uri.TryCreate(result?["projectUrl"]?.ToString(), UriKind.Absolute, out var repo) ? repo : null,
                TotalDownloads = int.TryParse(result?["totalDownloads"]?.ToString(), out var totalDownload) ? totalDownload : 0
            };
            return packageInfo;
        }

        public async Task<Stream> OnGetDownloadAsync(Guid sourceId, string packageId, string packageVersion)
        {
            var source = _dbContext.NugetSources.FindById(sourceId);
            if (source == null || string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(packageVersion))
                throw new FileNotFoundException("Invalid package or source.");

            using var handler = new HttpClientHandler();
            using var client = new HttpClient(handler);

            if (!string.IsNullOrWhiteSpace(source.UserName) && !string.IsNullOrWhiteSpace(source.Password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{source.UserName}:{source.Password}");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
            else if (!string.IsNullOrWhiteSpace(source.Password))
            {
                client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", source.Password);
            }

            var indexJson = await client.GetStringAsync(source.Url);
            var index = JsonNode.Parse(indexJson);

            var packageBaseUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "PackageBaseAddress/3.0.0")?["@id"]?.ToString();

            if (string.IsNullOrEmpty(packageBaseUrl))
                throw new FileNotFoundException("PackageBaseAddress not found.");

            var packageIdLower = packageId.ToLowerInvariant();
            var versionLower = packageVersion.ToLowerInvariant();
            var packageFileName = $"{packageIdLower}.{versionLower}.nupkg";
            var packageUrl = $"{packageBaseUrl.TrimEnd('/')}/{packageIdLower}/{versionLower}/{packageFileName}";

            try
            {
                return await client.GetStreamAsync(packageUrl);
            }
            catch (HttpRequestException ex)
            {
                throw new FileNotFoundException("Package could not be downloaded.", ex);
            }
        }
    }
}
