using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Services;

public class NugetService(DbContext dbContext) : INugetService
{
    public async Task<NugetPackageInfo?> GetPackageMetadataAsync(Guid sourceId, string packageId, string version, CancellationToken cancellationToken)
    {
        NugetSource source = dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using HttpClient client = CreateHttpClient(source);

        string indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        JsonNode? index = JsonNode.Parse(indexJson);

        string? registrationsUrl = ExtractResourceUrl(index, "RegistrationsBaseUrl");
        if (string.IsNullOrEmpty(registrationsUrl))
        {
            throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");
        }

        string registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
        string registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
        JsonArray? registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();

        if (registration is null)
        {
            return null;
        }

        NugetPackageInfo? packageInfo = FindPackageVersion(registration, packageId, version);
        return packageInfo;
    }

    public async Task<IEnumerable<NugetPackageInfo>> SearchPackagesAsync(
        Guid sourceId,
        string query,
        bool includePreRelease,
        CancellationToken cancellationToken)
    {
        NugetSource source = dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using HttpClient client = CreateHttpClient(source);

        string indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        JsonNode? index = JsonNode.Parse(indexJson);

        string? searchUrl = ExtractResourceUrl(index, "SearchQueryService");
        string? registrationsUrl = ExtractResourceUrl(index, "RegistrationsBaseUrl");

        if (string.IsNullOrEmpty(searchUrl) || string.IsNullOrEmpty(registrationsUrl))
        {
            throw new InvalidOperationException("Required NuGet endpoints not found in index.json");
        }

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

            if (!HasKamiYomuTag(result))
            {
                continue;
            }

            List<NugetPackageInfo> foundPackages = await ProcessPackageAsync(
                packageId,
                result,
                registrationsUrl,
                client,
                cancellationToken);

            packages.AddRange(foundPackages);
        }

        return packages;
    }

    public async Task<IEnumerable<NugetPackageInfo>> GetAllPackageVersionsAsync(Guid sourceId, string packageId, CancellationToken cancellationToken)
    {
        NugetSource source = dbContext.NugetSources.FindById(sourceId)
            ?? throw new InvalidOperationException("NuGet source not found.");

        using HttpClient client = CreateHttpClient(source);

        string indexJson = await client.GetStringAsync(source.Url, cancellationToken);
        JsonNode? index = JsonNode.Parse(indexJson);

        string? registrationsUrl = ExtractResourceUrl(index, "RegistrationsBaseUrl");
        if (string.IsNullOrEmpty(registrationsUrl))
        {
            throw new InvalidOperationException("RegistrationsBaseUrl not found in index.json");
        }

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

    private string? ExtractResourceUrl(JsonNode? index, string resourceType)
    {
        JsonArray? resources = index?["resources"]?.AsArray();
        if (resources is null)
        {
            return null;
        }

        JsonNode? resource = resources.FirstOrDefault(r =>
        {
            string? type = r?["@type"]?.ToString();
            if (type is null)
            {
                return false;
            }

            bool isExactMatch = type.Equals(resourceType, StringComparison.OrdinalIgnoreCase);
            bool isStartsWithMatch = type.StartsWith(resourceType, StringComparison.OrdinalIgnoreCase);

            return isExactMatch || isStartsWithMatch;
        });

        return resource?["@id"]?.ToString();
    }

    private bool HasKamiYomuTag(JsonNode? result)
    {
        string[] tags = ParseArray(result?["tags"]);
        return tags.Any(tag => tag.Equals(Package.KamiYomuCrawlerAgentTag, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<NugetPackageInfo>> ProcessPackageAsync(
        string packageId,
        JsonNode? searchResult,
        string registrationsUrl,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        List<NugetPackageInfo> packages = [];

        string registrationIndexUrl = $"{registrationsUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
        string registrationJson = await client.GetStringAsync(registrationIndexUrl, cancellationToken);
        JsonArray? registration = JsonNode.Parse(registrationJson)?["items"]?.AsArray();

        if (registration is null)
        {
            return packages;
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

                NugetPackageInfo packageInfo = BuildPackageInfo(packageId, searchResult, catalogEntry);

                string[] allDependencies = await ResolveCombinedDependenciesAsync(
                    catalogEntry,
                    registrationsUrl,
                    client,
                    cancellationToken);

                packageInfo.Dependencies.AddRange(allDependencies);

                packages.Add(packageInfo);
            }
        }

        return packages;
    }

    private async Task<string[]> ResolveCombinedDependenciesAsync(
        JsonNode catalogEntry,
        string registrationsUrl,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        string[] firstLevelDeps = ExtractDirectDependencies(catalogEntry);
        Dictionary<string, string[]> secondLevelDepsMap = await ExtractSecondLevelDependenciesAsync(
            firstLevelDeps,
            registrationsUrl,
            client,
            cancellationToken);

        List<string> allDependencies = new(firstLevelDeps);

        foreach (string[] secondLevelDeps in secondLevelDepsMap.Values)
        {
            allDependencies.AddRange(secondLevelDeps);
        }

        return [.. allDependencies.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private string[] ExtractDirectDependencies(JsonNode catalogEntry)
    {
        JsonArray? dependencyGroups = catalogEntry?["dependencyGroups"]?.AsArray();
        if (dependencyGroups is null)
        {
            return [];
        }

        return dependencyGroups
            .SelectMany(g => g?["dependencies"]?.AsArray() ?? [])
            .Select(d =>
            {
                string? id = d?["id"]?.ToString();
                string? range = d?["range"]?.ToString();

                if (string.IsNullOrEmpty(id))
                {
                    return null;
                }

                // Extract the minimum version from the range
                // Example: "[1.2.3]" → "1.2.3"
                string version = ExtractVersionFromRange(range);

                return $"{id}:{version}";
            })
            .Where(x => x is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static string ExtractVersionFromRange(string? range)
    {
        if (string.IsNullOrEmpty(range))
        {
            return "0.0.0";
        }

        // Remove brackets and parentheses
        string cleaned = range.Trim('[', ']', '(', ')');

        // Split by comma
        string[] parts = cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries);

        // First part is the minimum version
        string minVersion = parts.FirstOrDefault()?.Trim();

        return string.IsNullOrEmpty(minVersion) ? "0.0.0" : minVersion;
    }



    private async Task<Dictionary<string, string[]>> ExtractSecondLevelDependenciesAsync(
        string[] firstLevelDeps,
        string registrationsUrl,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> secondLevelDependencies = [];

        foreach (string depId in firstLevelDeps)
        {
            string registrationUrl = $"{registrationsUrl.TrimEnd('/')}/{depId.Replace(":", "/")}.json";

            try
            {
                string registrationJson = await client.GetStringAsync(registrationUrl, cancellationToken);
                JsonArray? dependencyGroups = JsonNode.Parse(registrationJson)?["catalogEntry"]?["dependencyGroups"]?.AsArray();

                if (dependencyGroups is not null)
                {
                    string[] deps = ExtractDepsFromDependencyGroups(dependencyGroups);
                    if (deps.Length > 0)
                    {
                        secondLevelDependencies[depId] = deps;
                    }
                }
            }
            catch
            {
                // Log or handle error silently for individual dependencies
            }
        }

        return secondLevelDependencies;
    }

    private string RemoveVersion(string dependency)
    {
        if (string.IsNullOrWhiteSpace(dependency))
        {
            return string.Empty;
        }
        else if (dependency.Contains(':'))
        {
            return dependency.Split(':')[0];
        }
        return dependency;
    }


    private string[] ExtractDepsFromDependencyGroups(JsonArray dependencyGroups)
    {
        HashSet<string> dependencies = new(StringComparer.OrdinalIgnoreCase);

        return dependencyGroups.SelectMany(g => g?["dependencies"]?.AsArray() ?? [])
                               .Select(d =>
                               {
                                   string? id = d?["id"]?.ToString();
                                   string? range = d?["range"]?.ToString();
                               
                                   if (string.IsNullOrEmpty(id))
                                   {
                                       return null;
                                   }
                               
                                   // Extract the minimum version from the range
                                   // Example: "[1.2.3]" → "1.2.3"
                                   string version = ExtractVersionFromRange(range);
                               
                                   return $"{id}:{version}";
                               })
                               .Where(x => x is not null)
                               .Distinct(StringComparer.OrdinalIgnoreCase)
                               .ToArray()!;
    }

    private NugetPackageInfo? FindPackageVersion(JsonArray registration, string packageId, string version)
    {
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
                    return BuildPackageInfo(packageId, null, catalogEntry);
                }
            }
        }

        return null;
    }

    private static HttpClient CreateHttpClient(NugetSource source)
    {
        HttpClient client = new();

        if (!string.IsNullOrEmpty(source.Password))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", source.Password);
        }

        return client;
    }

    private static NugetPackageInfo BuildPackageInfo(string packageId, JsonNode? searchResult, JsonNode catalogEntry)
    {
        return new NugetPackageInfo
        {
            Id = packageId,
            Version = catalogEntry?["version"]?.ToString() ?? string.Empty,
            Description = catalogEntry?["description"]?.ToString() ?? string.Empty,
            RepositoryUrl = TryUri(catalogEntry?["projectUrl"]),
            IconUrl = NugetPackageInfo.GetIconUri(catalogEntry, packageId),
            LicenseUrl = TryUri(catalogEntry?["licenseUrl"]),
            Authors = ParseArray(catalogEntry?["authors"]),
            Tags = ParseArray(searchResult?["tags"]),
            TotalDownloads = searchResult?["totalDownloads"]?.GetValue<long>() ?? 0L,
        };
    }

    private static string[] ParseArray(JsonNode? jsonNode)
    {
        return jsonNode is JsonArray jsonArray
            ? [.. jsonArray.Select(node => node?.ToString() ?? string.Empty).Where(str => !string.IsNullOrEmpty(str))]
            : jsonNode?.ToString()?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
    }

    private static Uri? TryUri(JsonNode? node)
    {
        return Uri.TryCreate(node?.ToString(), UriKind.Absolute, out Uri? uri) ? uri : null;
    }
}
