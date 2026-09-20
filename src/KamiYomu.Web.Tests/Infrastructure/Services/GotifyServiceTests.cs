using System.Net;
using System.Text.Json.Nodes;

using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;

using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class GotifyServiceTests
{
    [Fact]
    public async Task PushNotificationAsync_DoesNothing_WhenSettingsAreMissing()
    {
        using DbContext dbContext = new(":memory:");
        RecordingHttpMessageHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using HttpClient httpClient = new(handler);

        GotifyService service = new(Mock.Of<ILogger<GotifyService>>(), dbContext, new StubHttpClientFactory(httpClient));

        await service.PushNotificationAsync("hello", CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TestConnection_ReturnsTrue_WhenEndpointAcceptsNotification()
    {
        using DbContext dbContext = new(":memory:");
        RecordingHttpMessageHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using HttpClient httpClient = new(handler);

        GotifyService service = new(Mock.Of<ILogger<GotifyService>>(), dbContext, new StubHttpClientFactory(httpClient));
        GotifySettings settings = new(true, new Uri("https://gotify.example/"), "token-1");

        bool result = await service.TestConnection(settings, CancellationToken.None);

        Assert.True(result);
        _ = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://gotify.example/message?token=token-1", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task PushChapterDownloadedNotificationAsync_SendsChapterPayload()
    {
        ServiceTestHelpers.EnsureServiceLocatorConfigured();

        using DbContext dbContext = new(":memory:");
        GotifySettings settings = new(true, new Uri("https://gotify.example/"), "secret");
        _ = dbContext.UserPreferences.Insert(ServiceTestHelpers.CreateUserPreferenceWithGotify(settings));

        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(dbContext);
        string cbzPath = ServiceTestHelpers.CreateCbzFile(stored.Library, stored.Chapter, "001.png");

        RecordingHttpMessageHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using HttpClient httpClient = new(handler);

        try
        {
            GotifyService service = new(Mock.Of<ILogger<GotifyService>>(), dbContext, new StubHttpClientFactory(httpClient));

            await service.PushChapterDownloadedNotificationAsync(stored.ChapterDownload, CancellationToken.None);

            _ = Assert.Single(handler.Requests);
            JsonNode body = JsonNode.Parse(handler.Requests[0].Body)!;
            Assert.Equal(stored.Library.GetFilePathTemplateResolved(stored.Chapter), body["message"]?.ToString());
            Assert.Equal(cbzPath, body["extras"]?["chapter"]?["filePath"]?.ToString());
            Assert.Equal(stored.Library.Manga!.Title, body["extras"]?["chapter"]?["title"]?.ToString());
        }
        finally
        {
            ServiceTestHelpers.CleanupLibraryArtifacts(stored.Library, stored.Chapter);
        }
    }

    [Fact]
    public async Task PushSearchForChaptersCompletedNotificationAsync_SendsMangaPayload()
    {
        ServiceTestHelpers.EnsureServiceLocatorConfigured();

        using DbContext dbContext = new(":memory:");
        GotifySettings settings = new(true, new Uri("https://gotify.example/"), "secret");
        _ = dbContext.UserPreferences.Insert(ServiceTestHelpers.CreateUserPreferenceWithGotify(settings));

        StoredLibraryRecord stored = ServiceTestHelpers.CreateCompletedStoredChapter(dbContext);
        RecordingHttpMessageHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using HttpClient httpClient = new(handler);

        try
        {
            GotifyService service = new(Mock.Of<ILogger<GotifyService>>(), dbContext, new StubHttpClientFactory(httpClient));

            await service.PushSearchForChaptersCompletedNotificationAsync(stored.MangaDownload, CancellationToken.None);

            _ = Assert.Single(handler.Requests);
            JsonNode body = JsonNode.Parse(handler.Requests[0].Body)!;
            Assert.Equal(stored.Library.Manga!.Title, body["message"]?.ToString());
            Assert.Equal(stored.Library.GetMangaDirectory(), body["extras"]?["chapter"]?["mangaDirectory"]?.ToString());
            Assert.Equal(stored.MangaDownload.DownloadStatus.ToString(), body["extras"]?["chapter"]?["downloadStatus"]?.ToString());
        }
        finally
        {
            ServiceTestHelpers.CleanupLibraryArtifacts(stored.Library);
        }
    }
}
