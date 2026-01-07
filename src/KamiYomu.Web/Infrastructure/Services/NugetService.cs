using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities.Addons;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Services;

public class NugetService(DbContext dbContext) : INugetService
{
    public async Task<NugetPackageInfo?> GetPackageMetadataAsync(Guid sourceId, string packageId, string version, CancellationToken cancellationToken)
    {
        // Load source
        NugetSource source = dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using HttpClient client = CreateHttpClient(source);

        // Fetch service index
        string indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        JsonNode? index = JsonNode.Parse(indexJson);

        string? registrationsUrl = index?["resources"]?.AsArray()
            .FirstOrDefault(r => (r?["@type"]?.ToString()?.StartsWith("RegistrationsBaseUrl") ?? false))
            ?["@id"]?.ToString();

        if (string.IsNullOrEmpty(registrationsUrl))
        {
            throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");
        }

        // Fetch registration index for this package
        string registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
        string registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
        JsonArray? registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();
        if (registration is null)
        {
            return null;
        }

        // Look for the requested version
        foreach (JsonNode? page in registration)
        {
            JsonArray? versions = page?["items"]?.AsArray();
            if (versions is null)
            {
                continue;
            }

            foreach (JsonNode? versionEntry in versions)
            {
                JsonNode? catalogEntry = versionEntry?["catalogEntry"];
                string? currentVersion = catalogEntry?["version"]?.ToString();

                if (catalogEntry is null || string.IsNullOrEmpty(currentVersion))
                {
                    continue;
                }

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
        NugetSource source = dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using HttpClient client = CreateHttpClient(source);

        // Fetch service index
        string indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        JsonNode? index = JsonNode.Parse(indexJson);

        string? searchUrl = index?["resources"]?.AsArray()
            .FirstOrDefault(r => r?["@type"]?.ToString() == "SearchQueryService")?["@id"]?.ToString();

        string? registrationsUrl = index?["resources"]?.AsArray()
            .FirstOrDefault(r => r?["@type"]?.ToString()?.StartsWith("RegistrationsBaseUrl") ?? false)?["@id"]?.ToString();

        if (string.IsNullOrEmpty(searchUrl) || string.IsNullOrEmpty(registrationsUrl))
        {
            throw new InvalidOperationException("Required NuGet endpoints not found in index.json");
        }

        // Perform search
        string searchQueryUrl = $"{searchUrl}?q={Uri.EscapeDataString(query)}&prerelease={includePreRelease}&take=20";
        string searchJson = await client.GetStringAsync(searchQueryUrl, cancellationToken);
        JsonArray? searchResults = JsonNode.Parse(searchJson)?["data"]?.AsArray();

        List<NugetPackageInfo> packages = [];

        if (searchResults is null)
        {
            return packages;
        }

        foreach (JsonNode? result in searchResults)
        {
            string? packageId = result?["id"]?.ToString();
            if (string.IsNullOrEmpty(packageId))
            {
                continue;
            }

            // Tags filter
            if (searchUrl.Contains("nuget.org", StringComparison.OrdinalIgnoreCase))
            {
                string[] tags = ParseTags(result?["tags"]);
                if (!tags.Any(tag => tag.Equals(value: Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }


            // Fetch registration index
            string registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
            string registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
            JsonArray? registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();
            if (registration is null)
            {
                continue;
            }

            foreach (JsonNode? page in registration)
            {
                JsonArray? versions = page?["items"]?.AsArray();
                if (versions is null)
                {
                    continue;
                }

                foreach (JsonNode? versionEntry in versions)
                {
                    JsonNode? catalogEntry = versionEntry?["catalogEntry"];
                    if (catalogEntry is null)
                    {
                        continue;
                    }

                    NugetPackageInfo packageInfo = BuildPackageInfo(packageId, result, catalogEntry);
                    packages.Add(packageInfo);
                }
            }
        }

        return packages;
    }

    public async Task<IEnumerable<NugetPackageInfo>> GetAllPackageVersionsAsync(Guid sourceId, string packageId, CancellationToken cancellationToken)
    {
        // Load source
        NugetSource source = dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using HttpClient client = CreateHttpClient(source);

        // Fetch service index
        string indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        JsonNode? index = JsonNode.Parse(indexJson);

        string? registrationsUrl = index?["resources"]?.AsArray()
            .FirstOrDefault(r => (r?["@type"]?.ToString()?.StartsWith("RegistrationsBaseUrl") ?? false))
            ?["@id"]?.ToString();

        if (string.IsNullOrEmpty(registrationsUrl))
        {
            throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");
        }

        // Fetch registration index for this package
        string registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
        string registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
        JsonArray? registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();
        if (registration is null)
        {
            return [];
        }

        List<NugetPackageInfo> packages = [];

        foreach (JsonNode? page in registration)
        {
            JsonArray? versions = page?["items"]?.AsArray();
            if (versions is null)
            {
                continue;
            }

            foreach (JsonNode? versionEntry in versions)
            {
                JsonNode? catalogEntry = versionEntry?["catalogEntry"];
                if (catalogEntry is null)
                {
                    continue;
                }

                NugetPackageInfo packageInfo = BuildPackageInfo(packageId, null, catalogEntry);
                packages.Add(packageInfo);
            }
        }

        return packages;
    }
    private static HttpClient CreateHttpClient(NugetSource source)
    {
        HttpClientHandler handler = new();
        HttpClient client = new(handler);

        if (!string.IsNullOrWhiteSpace(source.UserName) && !string.IsNullOrWhiteSpace(source.Password))
        {
            byte[] byteArray = Encoding.ASCII.GetBytes($"{source.UserName}:{source.Password}");
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
            JsonArray array => [.. array.Select(t => t?.ToString()).Where(t => !string.IsNullOrWhiteSpace(t))],
            { } node when !string.IsNullOrWhiteSpace(node.ToString()) =>
                node.ToString().Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries),
            _ => []
        };

    private static NugetPackageInfo BuildPackageInfo(string packageId, JsonNode? result, JsonNode? catalogEntry)
    {
        string? version = catalogEntry?["version"]?.ToString();
        List<NugetDependencyInfo> dependencies = ParseDependencies(catalogEntry?["dependencyGroups"]);

        string[] authors = ParseAuthors(catalogEntry?["authors"]);

        return new NugetPackageInfo
        {
            Id = packageId,
            Version = version,
            IconUrl = TryUri(catalogEntry?["iconUrl"]),
            LicenseUrl = TryUri(catalogEntry?["licenseUrl"]),
            Description = catalogEntry?["description"]?.ToString(),
            Authors = authors,
            RepositoryUrl = TryUri(catalogEntry?["projectUrl"]),
            TotalDownloads = int.TryParse(catalogEntry?["totalDownloads"]?.ToString(), out int totalDownload) ? totalDownload : 0,
            Dependencies = dependencies
        };
    }

    private static List<NugetDependencyInfo> ParseDependencies(JsonNode? depsNode)
    {
        List<NugetDependencyInfo> dependencies = [];
        JsonArray? deps = depsNode?.AsArray();
        if (deps is null)
        {
            return dependencies;
        }

        foreach (JsonNode? group in deps)
        {
            string? targetFramework = group?["targetFramework"]?.ToString();
            JsonArray? groupDeps = group?["dependencies"]?.AsArray();
            if (groupDeps is null)
            {
                continue;
            }

            foreach (JsonNode? dep in groupDeps)
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
            ? [.. array.Select(p => p?.ToString()).Where(p => !string.IsNullOrEmpty(p))]
            : authorsNode?.ToString() is string singleAuthor && !string.IsNullOrEmpty(singleAuthor)
                ? new[] { singleAuthor }
                : [];

    private static Uri? TryUri(JsonNode? node) =>
        Uri.TryCreate(node?.ToString(), UriKind.Absolute, out Uri? uri) ? uri : null;

    public async Task<Stream[]> OnGetDownloadAsync(Guid sourceId, string packageId, string packageVersion, CancellationToken cancellationToken)
    {
        NugetSource source = dbContext.NugetSources.FindById(sourceId);
        if (source == null || string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(packageVersion))
        {
            throw new FileNotFoundException("Invalid package or source.");
        }

        using HttpClientHandler handler = new();
        using HttpClient client = new(handler);

        if (!string.IsNullOrWhiteSpace(source.UserName) && !string.IsNullOrWhiteSpace(source.Password))
        {
            byte[] byteArray = Encoding.ASCII.GetBytes($"{source.UserName}:{source.Password}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }
        else if (!string.IsNullOrWhiteSpace(source.Password))
        {
            client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", source.Password);
        }

        string indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        JsonNode? index = JsonNode.Parse(indexJson);

        string? packageBaseUrl = index?["resources"]?
            .AsArray()
            .FirstOrDefault(r => r?["@type"]?.ToString()?.StartsWith("PackageBaseAddress") ?? false)?["@id"]?.ToString();

        string? registrationBaseUrl = index?["resources"]?
            .AsArray()
            .FirstOrDefault(r => r?["@type"]?.ToString()?.StartsWith("RegistrationsBaseUrl") ?? false)?["@id"]?.ToString();

        if (string.IsNullOrEmpty(packageBaseUrl) || string.IsNullOrEmpty(registrationBaseUrl))
        {
            throw new FileNotFoundException("Required NuGet service endpoints not found.");
        }

        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        List<Stream> streams = [];

        async Task DownloadWithDependenciesAsync(string id, string version, bool mainPackage)
        {
            string key = $"{id.ToLowerInvariant()}:{version.ToLowerInvariant()}";
            if (!visited.Add(key))
            {
                return;
            }

            string cleanVersion = version
                .Split(',')[0]
                .Trim()
                .Trim('[', ']', '(', ')');

            string packageUrl = $"{packageBaseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}/{cleanVersion.ToLowerInvariant()}/{id.ToLowerInvariant()}.{cleanVersion.ToLowerInvariant()}.nupkg";
            Stream stream = await client.GetStreamAsync(packageUrl, cancellationToken);
            streams.Add(stream);

            string registrationUrl = $"{registrationBaseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}/index.json";
            using HttpResponseMessage response = await client.GetAsync(registrationUrl, cancellationToken);

            if (!response.IsSuccessStatusCode && mainPackage)
            {
                _ = response.EnsureSuccessStatusCode();
            }
            else
            {
                return;

            }

            using Stream regStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            JsonNode reg;
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                using GZipStream gzipStream = new(await response.Content.ReadAsStreamAsync(cancellationToken), CompressionMode.Decompress);
                using StreamReader reader = new(gzipStream);
                string regJson = await reader.ReadToEndAsync();
                reg = JsonNode.Parse(regJson);
            }
            else
            {
                string regJson = await response.Content.ReadAsStringAsync(cancellationToken);
                reg = JsonNode.Parse(regJson);
            }

            IEnumerable<JsonNode?>? entries = reg?["items"]?.AsArray()
                .SelectMany(item => item?["items"]?.AsArray() ?? [])
                .Where(entry => string.Equals(entry?["catalogEntry"]?["version"]?.ToString(), version, StringComparison.OrdinalIgnoreCase));

            foreach (JsonNode? entry in entries ?? [])
            {
                JsonArray? groups = entry?["catalogEntry"]?["dependencyGroups"]?.AsArray();
                if (groups == null)
                {
                    continue;
                }

                foreach (JsonNode? group in groups)
                {
                    JsonArray? dependencies = group?["dependencies"]?.AsArray();
                    if (dependencies == null)
                    {
                        continue;
                    }

                    foreach (JsonNode? dep in dependencies)
                    {
                        string? depId = dep?["id"]?.ToString();
                        string? depVersion = dep?["range"]?.ToString()?.Trim('[', ']');
                        if (!string.IsNullOrWhiteSpace(depId) && !string.IsNullOrWhiteSpace(depVersion))
                        {
                            await DownloadWithDependenciesAsync(depId, depVersion, false);
                        }
                    }
                }
            }
        }

        await DownloadWithDependenciesAsync(packageId, packageVersion, true);
        return [.. streams];
    }
}
