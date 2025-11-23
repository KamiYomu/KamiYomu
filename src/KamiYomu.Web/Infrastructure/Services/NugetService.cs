using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using System.IO.Compression;
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

        public async Task<NugetPackageInfo?> GetPackageMetadataAsync(string packageName, Guid sourceId, CancellationToken cancellationToken)
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
            var indexJson = await client.GetStringAsync(indexUrl, cancellationToken);
            var index = JsonNode.Parse(indexJson);

            var metadataUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "RegistrationsBaseUrl")?["@id"]?.ToString();

            if (string.IsNullOrEmpty(metadataUrl))
                throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");

            var packageUrl = $"{metadataUrl.TrimEnd('/')}/{packageName.ToLowerInvariant()}/index.json";
            var packageJson = await client.GetStringAsync(packageUrl, cancellationToken);
            var result = JsonNode.Parse(packageJson)?["items"]?.AsArray()?[0]?["items"]?.AsArray()?[0]?["catalogEntry"];

            if (result == null)
                return null;

            var packageTypes = result?["tags"]?.ToString();
            var isCrawlerAgent = packageTypes?.Contains(Defaults.Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase) == true;

            if (!isCrawlerAgent)
                return null;

            NugetPackageInfo packageInfo = ConvertToNuGetPackageInfo(result);
            return packageInfo;
        }

        public async Task<IEnumerable<NugetPackageInfo>> SearchPackagesAsync(string query, bool includePreRelease, Guid sourceId, CancellationToken cancellationToken)
        {
            var source = _dbContext.NugetSources.FindById(sourceId);
            if (source == null)
                throw new InvalidOperationException("NuGet source not found.");

            using var requestHandler = new HttpClientHandler();
            using var client = new HttpClient(requestHandler);

            // Authentication
            if (!string.IsNullOrWhiteSpace(source.UserName) && !string.IsNullOrWhiteSpace(source.Password))
            {
                var byteArray = Encoding.ASCII.GetBytes($"{source.UserName}:{source.Password}");
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }
            else if (!string.IsNullOrWhiteSpace(source.Password))
            {
                client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", source.Password);
            }

            // Fetch service index
            var indexJson = await client.GetStringAsync(source.Url, cancellationToken);
            var index = JsonNode.Parse(indexJson);

            var searchUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "SearchQueryService")?["@id"]?.ToString();

            var registrationsUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => (r?["@type"]?.ToString()?.StartsWith("RegistrationsBaseUrl") ?? false))
                ?["@id"]?.ToString();

            if (string.IsNullOrEmpty(searchUrl) || string.IsNullOrEmpty(registrationsUrl))
                throw new InvalidOperationException("Required NuGet endpoints not found in index.json");

            // Perform search
            var searchQueryUrl = $"{searchUrl}?q={Uri.EscapeDataString(query)}&prerelease={includePreRelease}&take=20";
            var searchJson = await client.GetStringAsync(searchQueryUrl, cancellationToken);
            var searchResults = JsonNode.Parse(searchJson)?["data"]?.AsArray();

            var packages = new List<NugetPackageInfo>();

            if (searchResults != null)
            {
                foreach (var result in searchResults)
                {
                    var packageId = result?["id"]?.ToString();
                    if (string.IsNullOrEmpty(packageId))
                        continue;

                    // Filter by tags if needed
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
                        tag.Equals(Defaults.Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase));

                    if (!isCrawlerAgent)
                        continue;

                    // Fetch registration index for full versions + dependencies
                    var registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
                    var registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
                    var registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();

                    if (registration == null)
                        continue;

                    foreach (var page in registration)
                    {
                        var versions = page?["items"]?.AsArray();
                        if (versions == null) continue;

                        foreach (var versionEntry in versions)
                        {
                            var catalogEntry = versionEntry?["catalogEntry"];
                            if (catalogEntry == null) continue;
                            NugetPackageInfo packageInfo = GetNugetPackageInfo(packageId, result, catalogEntry);
                            packages.Add(packageInfo);
                        }
                    }
                }
            }

            return packages;
        }

        private static NugetPackageInfo GetNugetPackageInfo(string packageId, JsonNode? result, JsonNode? catalogEntry)
        {
            var version = catalogEntry?["version"]?.ToString();
            var deps = catalogEntry?["dependencyGroups"]?.AsArray();

            var dependencies = new List<NugetDependencyInfo>();
            if (deps != null)
            {
                foreach (var group in deps)
                {
                    var targetFramework = group?["targetFramework"]?.ToString();
                    var groupDeps = group?["dependencies"]?.AsArray();
                    if (groupDeps == null) continue;

                    foreach (var dep in groupDeps)
                    {
                        dependencies.Add(new NugetDependencyInfo
                        {
                            Id = dep?["id"]?.ToString(),
                            VersionRange = dep?["range"]?.ToString(),
                            TargetFramework = targetFramework
                        });
                    }
                }
            }
            var authorsNode = result?["authors"];

            var authors = authorsNode is JsonArray array
                ? array.Select(p => p?.ToString()).Where(p => !string.IsNullOrEmpty(p)).ToArray()
                : authorsNode?.ToString() is string singleAuthor && !string.IsNullOrEmpty(singleAuthor)
                    ? [singleAuthor]
                    : Array.Empty<string>();

            var packageInfo = new NugetPackageInfo
            {
                Id = packageId,
                Version = version,
                IconUrl = Uri.TryCreate(result?["iconUrl"]?.ToString(), UriKind.Absolute, out var icon) ? icon : null,
                LicenseUrl = Uri.TryCreate(result?["licenseUrl"]?.ToString(), UriKind.Absolute, out var licenseUrl) ? licenseUrl : null,
                Description = result?["description"]?.ToString(),
                Authors = authors,
                RepositoryUrl = Uri.TryCreate(result?["projectUrl"]?.ToString(), UriKind.Absolute, out var repo) ? repo : null,
                TotalDownloads = int.TryParse(result?["totalDownloads"]?.ToString(), out var totalDownload) ? totalDownload : 0,
                Dependencies = dependencies
            };
            return packageInfo;
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

        public async Task<Stream[]> OnGetDownloadAsync(Guid sourceId, string packageId, string packageVersion, CancellationToken cancellationToken)
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

            var indexJson = await client.GetStringAsync(source.Url, cancellationToken);
            var index = JsonNode.Parse(indexJson);

            var packageBaseUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "PackageBaseAddress/3.0.0")?["@id"]?.ToString();

            var registrationBaseUrl = index?["resources"]?
                .AsArray()
                .FirstOrDefault(r => r?["@type"]?.ToString() == "RegistrationsBaseUrl/3.6.0")?["@id"]?.ToString();

            if (string.IsNullOrEmpty(packageBaseUrl) || string.IsNullOrEmpty(registrationBaseUrl))
                throw new FileNotFoundException("Required NuGet service endpoints not found.");

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var streams = new List<Stream>();

            async Task DownloadWithDependenciesAsync(string id, string version)
            {
                var key = $"{id.ToLowerInvariant()}:{version.ToLowerInvariant()}";
                if (!visited.Add(key)) return;

                var cleanVersion = version
                    .Split(',')[0]
                    .Trim()
                    .Trim('[', ']', '(', ')');

                var packageUrl = $"{packageBaseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}/{cleanVersion.ToLowerInvariant()}/{id.ToLowerInvariant()}.{cleanVersion.ToLowerInvariant()}.nupkg";
                var stream = await client.GetStreamAsync(packageUrl, cancellationToken);
                streams.Add(stream);

                var registrationUrl = $"{registrationBaseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}/index.json";
                using var response = await client.GetAsync(registrationUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var regStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var gzipStream = new GZipStream(regStream, CompressionMode.Decompress);
                using var reader = new StreamReader(gzipStream);
                var regJson = await reader.ReadToEndAsync();
                var reg = JsonNode.Parse(regJson);

                var entries = reg?["items"]?.AsArray()
                    .SelectMany(item => item?["items"]?.AsArray() ?? new JsonArray())
                    .Where(entry => string.Equals(entry?["catalogEntry"]?["version"]?.ToString(), version, StringComparison.OrdinalIgnoreCase));

                foreach (var entry in entries ?? Enumerable.Empty<JsonNode>())
                {
                    var groups = entry?["catalogEntry"]?["dependencyGroups"]?.AsArray();
                    if (groups == null) continue;

                    foreach (var group in groups)
                    {
                        var dependencies = group?["dependencies"]?.AsArray();
                        if (dependencies == null) continue;

                        foreach (var dep in dependencies)
                        {
                            var depId = dep?["id"]?.ToString();
                            var depVersion = dep?["range"]?.ToString()?.Trim('[', ']');
                            if (!string.IsNullOrWhiteSpace(depId) && !string.IsNullOrWhiteSpace(depVersion))
                            {
                                await DownloadWithDependenciesAsync(depId, depVersion);
                            }
                        }
                    }
                }
            }

            await DownloadWithDependenciesAsync(packageId, packageVersion);
            return streams.ToArray();
        }
    }
}
