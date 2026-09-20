using System.Net;

using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class GitHubServiceTests
{
    [Fact]
    public async Task GetLatestVersionAsync_UsesGitHubResponseAndCachesResult()
    {
        ServiceTestHelpers.InitializeCache(nameof(GitHubServiceTests));

        RecordingHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{ "tag_name": "v2.3.4" }]""")
            }));

        using HttpClient httpClient = new(handler);
        using GitHubService service = new(new StubHttpClientFactory(httpClient), new CacheContext());

        string first = await service.GetLatestVersionAsync(CancellationToken.None);
        string second = await service.GetLatestVersionAsync(CancellationToken.None);

        Assert.Equal("v2.3.4", first);
        Assert.Equal(first, second);
        _ = Assert.Single(handler.Requests);
        Assert.Equal("https://api.github.com/repos/KamiYomu/KamiYomu/releases", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsTrue_WhenLatestVersionIsGreater()
    {
        ServiceTestHelpers.InitializeCache($"{nameof(GitHubServiceTests)}-updates");

        RecordingHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{ "tag_name": "v1.2.0" }]""")
            }));

        using HttpClient httpClient = new(handler);
        using GitHubService service = new(new StubHttpClientFactory(httpClient), new CacheContext());

        bool result = await service.CheckForUpdatesAsync("1.1.0", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsFalse_WhenCurrentVersionIsInvalid()
    {
        ServiceTestHelpers.InitializeCache($"{nameof(GitHubServiceTests)}-invalid");

        RecordingHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{ "tag_name": "v1.2.0" }]""")
            }));

        using HttpClient httpClient = new(handler);
        using GitHubService service = new(new StubHttpClientFactory(httpClient), new CacheContext());

        bool result = await service.CheckForUpdatesAsync("not-a-version", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        ServiceTestHelpers.InitializeCache($"{nameof(GitHubServiceTests)}-dispose");

        RecordingHttpMessageHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using HttpClient httpClient = new(handler);
        GitHubService service = new(new StubHttpClientFactory(httpClient), new CacheContext());

        service.Dispose();
        service.Dispose();
    }
}
