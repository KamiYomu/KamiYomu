using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace KamiYomu.Web.Infrastructure.Services;

public class NugetService : INugetService
{
    private readonly DbContext _dbContext;

    public NugetService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<NugetPackageInfo?> GetPackageMetadataAsync(Guid sourceId, string packageId, string version, CancellationToken cancellationToken)
    {
        // Load source
        var source = _dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using var client = CreateHttpClient(source);

        // Fetch service index
        var indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        var index = JsonNode.Parse(indexJson);

        var registrationsUrl = index?["resources"]?.AsArray()
            .FirstOrDefault(r => (r?["@type"]?.ToString()?.StartsWith("RegistrationsBaseUrl") ?? false))
            ?["@id"]?.ToString();

        if (string.IsNullOrEmpty(registrationsUrl))
            throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");

        // Fetch registration index for this package
        var registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
        var registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
        var registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();
        if (registration is null) return null;

        // Look for the requested version
        foreach (var page in registration)
        {
            var versions = page?["items"]?.AsArray();
            if (versions is null) continue;

            foreach (var versionEntry in versions)
            {
                var catalogEntry = versionEntry?["catalogEntry"];
                var currentVersion = catalogEntry?["version"]?.ToString();

                if (catalogEntry is null || string.IsNullOrEmpty(currentVersion))
                    continue;

                if (string.Equals(currentVersion, version, StringComparison.OrdinalIgnoreCase))
                {
                    // Build and return the package info for this version
                    return BuildPackageInfo(packageId, null, catalogEntry);
                }
            }
        }

        // Not found
        return null;
    }

    public async Task<IEnumerable<NugetPackageInfo>> SearchPackagesAsync(Guid sourceId, string query, bool includePreRelease, CancellationToken cancellationToken)
    {
        var source = _dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using var client = CreateHttpClient(source);

        // Fetch service index
        var indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        var index = JsonNode.Parse(indexJson);

        var searchUrl = index?["resources"]?.AsArray()
            .FirstOrDefault(r => r?["@type"]?.ToString() == "SearchQueryService")?["@id"]?.ToString();

        var registrationsUrl = index?["resources"]?.AsArray()
            .FirstOrDefault(r => r?["@type"]?.ToString()?.StartsWith("RegistrationsBaseUrl") ?? false)?["@id"]?.ToString();

        if (string.IsNullOrEmpty(searchUrl) || string.IsNullOrEmpty(registrationsUrl))
            throw new InvalidOperationException("Required NuGet endpoints not found in index.json");

        // Perform search
        var searchQueryUrl = $"{searchUrl}?q={Uri.EscapeDataString(query)}&prerelease={includePreRelease}&take=20";
        var searchJson = await client.GetStringAsync(searchQueryUrl, cancellationToken);
        var searchResults = JsonNode.Parse(searchJson)?["data"]?.AsArray();

        var packages = new List<NugetPackageInfo>();

        if (searchResults is null) return packages;

        foreach (var result in searchResults)
        {
            var packageId = result?["id"]?.ToString();
            if (string.IsNullOrEmpty(packageId)) continue;

            // Tags filter
            var tags = ParseTags(result?["tags"]);
            if (!tags.Any(tag => tag.Equals(Defaults.Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Fetch registration index
            var registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
            var registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
            var registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();
            if (registration is null) continue;

            foreach (var page in registration)
            {
                var versions = page?["items"]?.AsArray();
                if (versions is null) continue;

                foreach (var versionEntry in versions)
                {
                    var catalogEntry = versionEntry?["catalogEntry"];
                    if (catalogEntry is null) continue;

                    var packageInfo = BuildPackageInfo(packageId, result, catalogEntry);
                    packages.Add(packageInfo);
                }
            }
        }

        return packages;
    }

    public async Task<IEnumerable<NugetPackageInfo>> GetAllPackageVersionsAsync(Guid sourceId, string packageId, CancellationToken cancellationToken)
    {
        // Load source
        var source = _dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using var client = CreateHttpClient(source);

        // Fetch service index
        var indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        var index = JsonNode.Parse(indexJson);

        var registrationsUrl = index?["resources"]?.AsArray()
            .FirstOrDefault(r => (r?["@type"]?.ToString()?.StartsWith("RegistrationsBaseUrl") ?? false))
            ?["@id"]?.ToString();

        if (string.IsNullOrEmpty(registrationsUrl))
            throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");

        // Fetch registration index for this package
        var registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
        var registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
        var registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();
        if (registration is null) return new List<NugetPackageInfo>();

        var packages = new List<NugetPackageInfo>();

        foreach (var page in registration)
        {
            var versions = page?["items"]?.AsArray();
            if (versions is null) continue;

            foreach (var versionEntry in versions)
            {
                var catalogEntry = versionEntry?["catalogEntry"];
                if (catalogEntry is null) continue;

                var packageInfo = BuildPackageInfo(packageId, null, catalogEntry);
                packages.Add(packageInfo);
            }
        }

        return packages;
    }
    private static HttpClient CreateHttpClient(NugetSource source)
    {
        var handler = new HttpClientHandler();
        var client = new HttpClient(handler);

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

        return client;
    }

    private static string[] ParseTags(JsonNode? tagsNode) =>
        tagsNode switch
        {
            JsonArray array => array.Select(t => t?.ToString())
                                    .Where(t => !string.IsNullOrWhiteSpace(t))
                                    .ToArray(),
            { } node when !string.IsNullOrWhiteSpace(node.ToString()) =>
                node.ToString().Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries),
            _ => Array.Empty<string>()
        };

    private static NugetPackageInfo BuildPackageInfo(string packageId, JsonNode? result, JsonNode? catalogEntry)
    {
        var version = catalogEntry?["version"]?.ToString();
        var dependencies = ParseDependencies(catalogEntry?["dependencyGroups"]);

        var authors = ParseAuthors(catalogEntry?["authors"]);

        return new NugetPackageInfo
        {
            Id = packageId,
            Version = version,
            IconUrl = TryUri(catalogEntry?["iconUrl"]),
            LicenseUrl = TryUri(catalogEntry?["licenseUrl"]),
            Description = catalogEntry?["description"]?.ToString(),
            Authors = authors,
            RepositoryUrl = TryUri(catalogEntry?["projectUrl"]),
            TotalDownloads = int.TryParse(catalogEntry?["totalDownloads"]?.ToString(), out var totalDownload) ? totalDownload : 0,
            Dependencies = dependencies
        };
    }

    private static List<NugetDependencyInfo> ParseDependencies(JsonNode? depsNode)
    {
        var dependencies = new List<NugetDependencyInfo>();
        var deps = depsNode?.AsArray();
        if (deps is null) return dependencies;

        foreach (var group in deps)
        {
            var targetFramework = group?["targetFramework"]?.ToString();
            var groupDeps = group?["dependencies"]?.AsArray();
            if (groupDeps is null) continue;

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

        return dependencies;
    }

    private static string[] ParseAuthors(JsonNode? authorsNode) =>
        authorsNode is JsonArray array
            ? array.Select(p => p?.ToString()).Where(p => !string.IsNullOrEmpty(p)).ToArray()
            : authorsNode?.ToString() is string singleAuthor && !string.IsNullOrEmpty(singleAuthor)
                ? new[] { singleAuthor }
                : Array.Empty<string>();

    private static Uri? TryUri(JsonNode? node) =>
        Uri.TryCreate(node?.ToString(), UriKind.Absolute, out var uri) ? uri : null;

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
