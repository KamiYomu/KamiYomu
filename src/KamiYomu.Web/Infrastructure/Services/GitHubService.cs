using System.Text.Json;

using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Infrastructure.Services;

public class GitHubService(IHttpClientFactory httpClientFactory) : IDisposable, IGitHubService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(AppOptions.Defaults.Worker.HttpClientApp);
    private bool disposedValue;
    private const string Owner = "KamiYomu";
    private const string Repo = "KamiYomu";
    public async Task<string> GetLatestVersionAsync(CancellationToken cancellationToken)
    {

        string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases";

        try
        {
            string response = await _httpClient.GetStringAsync(url, cancellationToken);

            JsonElement releases = JsonSerializer.Deserialize<JsonElement>(response);
            if (releases.GetArrayLength() > 0)
            {
                JsonElement latest = releases[0];
                string? tagName = latest.GetProperty("tag_name").GetString();
                return tagName ?? "No version found";
            }

            return "No releases found";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken)
    {
        string latestVersion = await GetLatestVersionAsync(cancellationToken);

        if (string.IsNullOrEmpty(latestVersion))
        {
            return false;
        }

        // Compare versions
        try
        {
            Version current = new(currentVersion);
            Version latest = new(latestVersion.TrimStart('v')); // Remove leading 'v' if present
            return latest > current;
        }
        catch
        {
            // If version parsing fails, assume no update
            return false;
        }
    }


    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _httpClient.Dispose();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
