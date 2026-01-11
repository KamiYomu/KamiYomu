using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Infrastructure.Services;
public sealed class GotifyService(
    ILogger<GotifyService> logger,
    DbContext dbContext,
    IHttpClientFactory httpClientFactory)
    : IGotifyService
{
    private readonly Lazy<HttpClient> _httpClient = new(() =>
    {
        HttpClient client = httpClientFactory.CreateClient(
            AppOptions.Defaults.Integrations.HttpClientApp);

        UserPreference preferences = dbContext.UserPreferences.Query()
            .Include(p => p.GotifySettings)
            .FirstOrDefault();

        if (preferences?.GotifySettings?.ServiceUri != null)
        {
            client.BaseAddress = preferences.GotifySettings.ServiceUri;
        }

        return client;
    });

    public async Task PushNotificationAsync(
        string message,
        CancellationToken cancellationToken)
    {
        GotifySettings? settings = dbContext.UserPreferences.Query().Include(p => p.GotifySettings).FirstOrDefault()?.GotifySettings;
        if (settings == null)
        {
            logger.LogWarning("Gotify settings not configured");
            return;
        }

        HttpRequestMessage request = CreateMessageRequest(
            settings,
            title: "Notification",
            message: message,
            priority: 5);

        HttpResponseMessage response = await _httpClient.Value.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogError(
                "Gotify push failed ({StatusCode}): {Response}",
                response.StatusCode,
                content);

            _ = response.EnsureSuccessStatusCode();
        }
    }

    public async Task<bool> TestConnection(
        GotifySettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpRequestMessage request = CreateMessageRequest(
                settings,
                title: "Connection Test",
                message: "Gotify connection successful",
                priority: 1);

            HttpResponseMessage response = await _httpClient.Value.SendAsync(
                request,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gotify connection test failed");
            return false;
        }
    }



    private static HttpRequestMessage CreateMessageRequest(
        GotifySettings settings,
        string title,
        string message,
        int priority,
        object? extras = null)
    {
        Uri uri = new(
            settings.ServiceUri,
            $"/message?token={settings.ApiKey}");

        var payload = new
        {
            title,
            message,
            priority,
            extras
        };

        HttpRequestMessage request = new(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(payload)
        };

        return request;
    }

    public async Task PushChapterDownloadedNotificationAsync(
    ChapterDownloadRecord chapterDownload,
    CancellationToken cancellationToken)
    {
        GotifySettings? settings = dbContext.UserPreferences
            .Query()
            .Include(p => p.GotifySettings)
            .FirstOrDefault()
            ?.GotifySettings;

        if (settings == null)
        {
            logger.LogWarning("Gotify settings not configured");
            return;
        }

        var extras = new
        {
            chapter = new
            {
                chapterDownload.Id,
                chapterDownload.Chapter.Number,
                chapterDownload.MangaDownload.Library.Manga.Title,
                chapterDownload.Chapter.Uri,
                FilePath = chapterDownload.MangaDownload.Library.GetCbzFilePath(chapterDownload.Chapter),
                DownloadStatus = chapterDownload.DownloadStatus.ToString()
            }
        };

        HttpRequestMessage request = CreateMessageRequest(
            settings,
            title: I18n.ChapterDownloaded,
            message: chapterDownload.MangaDownload.Library.GetFilePathTemplateResolved(chapterDownload.Chapter),
            priority: 5,
            extras: extras);

        HttpResponseMessage response = await _httpClient.Value.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogError(
                "Gotify push failed ({StatusCode}): {Response}",
                response.StatusCode,
                content);

            _ = response.EnsureSuccessStatusCode();
        }
    }


    public async Task PushSearchForChaptersCompletedNotificationAsync(
    MangaDownloadRecord mangaDownload,
    CancellationToken cancellationToken)
    {
        GotifySettings? settings = dbContext.UserPreferences
            .Query()
            .Include(p => p.GotifySettings)
            .FirstOrDefault()
            ?.GotifySettings;

        if (settings == null)
        {
            logger.LogWarning("Gotify settings not configured");
            return;
        }

        var extras = new
        {
            chapter = new
            {
                mangaDownload.Id,
                mangaDownload.Library.Manga.Title,
                mangaDownload.Library.Manga.CoverUrl,
                mangaDownload.Library.Manga.Tags,
                MangaDirectory = mangaDownload.Library.GetMangaDirectory(),
                DownloadStatus = mangaDownload.DownloadStatus.ToString()
            }
        };

        HttpRequestMessage request = CreateMessageRequest(
            settings,
            title: I18n.SearchForChaptersCompleted,
            message: mangaDownload.Library.Manga.Title,
            priority: 5,
            extras: extras);

        HttpResponseMessage response = await _httpClient.Value.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogError(
                "Gotify push failed ({StatusCode}): {Response}",
                response.StatusCode,
                content);

            _ = response.EnsureSuccessStatusCode();
        }
    }

}

