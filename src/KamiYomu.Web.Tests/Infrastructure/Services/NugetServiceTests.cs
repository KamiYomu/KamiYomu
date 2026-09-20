using System.Net;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Settings.Models;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class NugetServiceTests
{
    [Fact]
    public async Task GetPackageMetadataAsync_ReturnsRequestedVersionMetadata()
    {
        using LocalHttpServer server = CreateServer();
        using DbContext dbContext = new(":memory:");
        NugetSource source = new("Local", new Uri(server.BaseUri, "index.json"), null, null);
        ServiceTestHelpers.AssignId(source);
        _ = dbContext.NugetSources.Insert(source);

        NugetService service = new(dbContext, Options.Create(new StartupOptions()));

        NugetPackageInfo? result = await service.GetPackageMetadataAsync(source.Id, "Test.Package", "1.0.0", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Test.Package", result.Id);
        Assert.Equal("1.0.0", result.Version);
        Assert.Equal("Test package", result.Description);
    }

    [Fact]
    public async Task SearchPackagesAsync_ReturnsOnlyKamiYomuTaggedPackages_WithResolvedDependencies()
    {
        using LocalHttpServer server = CreateServer();
        using DbContext dbContext = new(":memory:");
        NugetSource source = new("Local", new Uri(server.BaseUri, "index.json"), null, null);
        ServiceTestHelpers.AssignId(source);
        _ = dbContext.NugetSources.Insert(source);

        NugetService service = new(dbContext, Options.Create(new StartupOptions()));

        IEnumerable<NugetPackageInfo> results = await service.SearchPackagesAsync(source.Id, "Test", includePreRelease: true, CancellationToken.None);

        // The registration fixture (shared with GetAllPackageVersionsAsync_ReturnsAllVersions)
        // has two versions of Test.Package, and SearchPackagesAsync returns one NugetPackageInfo
        // per version found in the registration (same as GetAllPackageVersionsAsync), so both are
        // expected here -- only the "Ignored.Package" result (missing the kamiyomu tag) is filtered out.
        Assert.All(results, package => Assert.Equal("Test.Package", package.Id));
        Assert.Equal(new[] { "1.0.0", "2.0.0" }, results.Select(x => x.Version).ToArray());

        NugetPackageInfo package = Assert.Single(results, x => x.Version == "1.0.0");
        Assert.Contains("KamiYomu.CrawlerAgents.Core:1.2.3", package.Dependencies);
        Assert.Contains("Dep.One:2.0.0", package.Dependencies);
        Assert.Contains("Transitive.Dep:3.0.0", package.Dependencies);
    }

    [Fact]
    public async Task GetAllPackageVersionsAsync_ReturnsAllVersions()
    {
        using LocalHttpServer server = CreateServer();
        using DbContext dbContext = new(":memory:");
        NugetSource source = new("Local", new Uri(server.BaseUri, "index.json"), null, null);
        ServiceTestHelpers.AssignId(source);
        _ = dbContext.NugetSources.Insert(source);

        NugetService service = new(dbContext, Options.Create(new StartupOptions()));

        IEnumerable<NugetPackageInfo> results = await service.GetAllPackageVersionsAsync(source.Id, "Test.Package", CancellationToken.None);

        Assert.Equal(new[] { "1.0.0", "2.0.0" }, results.Select(x => x.Version).ToArray());
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsPackageStreams_ForPackageAndDependency()
    {
        using LocalHttpServer server = CreateServer();
        using DbContext dbContext = new(":memory:");
        NugetSource source = new("Local", new Uri(server.BaseUri, "index.json"), null, null);
        ServiceTestHelpers.AssignId(source);
        _ = dbContext.NugetSources.Insert(source);

        NugetService service = new(dbContext, Options.Create(new StartupOptions
        {
            MaximumPackageDependencyDepth = 2
        }));

        Stream[] streams = await service.OnGetDownloadAsync(source.Id, "Test.Package", "1.0.0", CancellationToken.None);

        try
        {
            Assert.Equal(2, streams.Length);
            Assert.All(streams, stream => Assert.True(stream.CanRead));
        }
        finally
        {
            foreach (Stream stream in streams)
            {
                stream.Dispose();
            }
        }
    }

    private static LocalHttpServer CreateServer()
    {
        Dictionary<string, Func<HttpListenerRequest, HttpResponseData>> routes = new(StringComparer.OrdinalIgnoreCase);
        LocalHttpServer server = new(routes);
        string baseUrl = server.BaseUri.ToString().TrimEnd('/');

        routes["/index.json"] = _ => HttpResponseData.Json($$"""
        {
          "resources": [
            { "@type": "RegistrationsBaseUrl", "@id": "{{baseUrl}}/registrations/" },
            { "@type": "SearchQueryService", "@id": "{{baseUrl}}/search" },
            { "@type": "PackageBaseAddress", "@id": "{{baseUrl}}/packagebase/" }
          ]
        }
        """);

        routes["/search?q=Test&prerelease=True&take=20"] = _ => HttpResponseData.Json("""
        {
          "data": [
            {
              "id": "Test.Package",
              "tags": [ "kamiyomu-crawler-agents", "featured" ],
              "totalDownloads": 123
            },
            {
              "id": "Ignored.Package",
              "tags": [ "other" ],
              "totalDownloads": 99
            }
          ]
        }
        """);
        routes["/search?q=Test&prerelease=true&take=20"] = routes["/search?q=Test&prerelease=True&take=20"];

        routes["/registrations/test.package/index.json"] = _ => HttpResponseData.Json("""
        {
          "items": [
            {
              "items": [
                {
                  "catalogEntry": {
                    "version": "1.0.0",
                    "description": "Test package",
                    "authors": "KamiYomu",
                    "projectUrl": "https://example.com/project",
                    "licenseUrl": "https://example.com/license",
                    "dependencyGroups": [
                      {
                        "dependencies": [
                          { "id": "KamiYomu.CrawlerAgents.Core", "range": "[1.2.3]" },
                          { "id": "Dep.One", "range": "[2.0.0]" }
                        ]
                      }
                    ]
                  }
                },
                {
                  "catalogEntry": {
                    "version": "2.0.0",
                    "description": "Test package v2",
                    "authors": "KamiYomu",
                    "projectUrl": "https://example.com/project",
                    "licenseUrl": "https://example.com/license",
                    "dependencyGroups": []
                  }
                }
              ]
            }
          ]
        }
        """);

        routes["/registrations/kamiyomu.crawleragents.core/1.2.3.json"] = _ => HttpResponseData.Json("""
        {
          "catalogEntry": {
            "dependencyGroups": []
          }
        }
        """);

        routes["/registrations/dep.one/2.0.0.json"] = _ => HttpResponseData.Json("""
        {
          "catalogEntry": {
            "dependencyGroups": [
              {
                "dependencies": [
                  { "id": "Transitive.Dep", "range": "[3.0.0]" }
                ]
              }
            ]
          }
        }
        """);

        routes["/registrations/test.package/1.0.0.json"] = _ => HttpResponseData.Json("""
        {
          "catalogEntry": {
            "dependencyGroups": [
              {
                "dependencies": [
                  { "id": "Dep.Package", "range": "[2.0.0]" }
                ]
              }
            ]
          }
        }
        """);

        routes["/registrations/dep.package/2.0.0.json"] = _ => HttpResponseData.Json("""
        {
          "catalogEntry": {
            "dependencyGroups": []
          }
        }
        """);

        routes["/packagebase/test.package/1.0.0/test.package.1.0.0.nupkg"] =
            _ => HttpResponseData.Bytes([1, 2, 3, 4]);

        routes["/packagebase/dep.package/2.0.0/dep.package.2.0.0.nupkg"] =
            _ => HttpResponseData.Bytes([5, 6, 7, 8]);

        return server;
    }
}
