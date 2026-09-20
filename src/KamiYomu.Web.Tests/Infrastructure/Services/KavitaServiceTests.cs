using System.Net;

using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;

using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class KavitaServiceTests
{
    [Fact]
    public async Task LoadAllCollectionsAsync_ReturnsEmpty_WhenSettingsAreMissing()
    {
        using DbContext dbContext = new(":memory:");
        RecordingHttpMessageHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using HttpClient httpClient = new(handler);

        KavitaService service = new(Mock.Of<ILogger<KavitaService>>(), dbContext, new StubHttpClientFactory(httpClient));

        IReadOnlyList<KavitaLibrary> result = await service.LoadAllCollectionsAsync(CancellationToken.None);

        Assert.Empty(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task LoadAllCollectionsAsync_UsesApiKeyAuthentication_AndReturnsLibraries()
    {
        using DbContext dbContext = new(":memory:");
        KavitaSettings settings = new(new Uri("https://kavita.example/"), "", "", "api-key", true);
        _ = dbContext.UserPreferences.Insert(ServiceTestHelpers.CreateUserPreferenceWithKavita(settings));

        RecordingHttpMessageHandler handler = new((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/Plugin/authenticate")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "token": "bearer-token" }""")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{ "id": 1, "name": "Library One", "type": 2 }]""")
            });
        });

        using HttpClient httpClient = new(handler);
        KavitaService service = new(Mock.Of<ILogger<KavitaService>>(), dbContext, new StubHttpClientFactory(httpClient));

        IReadOnlyList<KavitaLibrary> result = await service.LoadAllCollectionsAsync(CancellationToken.None);

        _ = Assert.Single(result);
        Assert.Equal("Library One", result[0].Name);
        Assert.Equal("/api/Plugin/authenticate", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.StartsWith("Bearer ", handler.Requests[1].Headers["Authorization"].Single());
    }

    [Fact]
    public async Task UpdateAllCollectionsAsync_UsesUsernamePasswordAuthentication()
    {
        using DbContext dbContext = new(":memory:");
        KavitaSettings settings = new(new Uri("https://kavita.example/"), "user", "password", "", true);
        _ = dbContext.UserPreferences.Insert(ServiceTestHelpers.CreateUserPreferenceWithKavita(settings));

        RecordingHttpMessageHandler handler = new((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/Account/login")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "token": "login-token" }""")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using HttpClient httpClient = new(handler);
        KavitaService service = new(Mock.Of<ILogger<KavitaService>>(), dbContext, new StubHttpClientFactory(httpClient));

        await service.UpdateAllCollectionsAsync(CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/api/Account/login", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/api/Library/scan-all", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.StartsWith("Bearer ", handler.Requests[1].Headers["Authorization"].Single());
    }

    [Fact]
    public async Task TestConnection_ReturnsFalse_WhenAuthenticationFails()
    {
        using DbContext dbContext = new(":memory:");
        RecordingHttpMessageHandler handler = new((_, _) => throw new HttpRequestException("boom"));
        using HttpClient httpClient = new(handler);

        KavitaService service = new(Mock.Of<ILogger<KavitaService>>(), dbContext, new StubHttpClientFactory(httpClient));
        KavitaSettings settings = new(new Uri("https://kavita.example/"), "", "", "api-key", true);

        bool result = await service.TestConnection(settings, CancellationToken.None);

        Assert.False(result);
    }
}
