using System.Net.Http.Headers;

using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Infrastructure.Services;
public sealed class KavitaService(
    ILogger<KavitaService> logger,
    DbContext dbContext,
    IHttpClientFactory httpClientFactory
) : IKavitaService
{
    private readonly DbContext _dbContext = dbContext;
    private readonly ILogger<KavitaService> _logger = logger;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;


    private HttpClient CreateClient(KavitaSettings settings)
    {
        HttpClient client = _httpClientFactory.CreateClient();
        client.BaseAddress = settings.ServiceUri;
        return client;
    }

    public async Task<IReadOnlyList<KavitaLibrary>> LoadAllCollectionsAsync(CancellationToken cancellationToken)
    {
        KavitaSettings? settings = _dbContext.UserPreferences.Include(p => p.KavitaSettings).Query()
            .FirstOrDefault()?.KavitaSettings;

        if (settings == null)
        {
            return [];
        }

        using HttpClient client = CreateClient(settings);
        await AttachAuthenticationAsync(client, settings, cancellationToken);

        using HttpResponseMessage response = await client.GetAsync("/api/Library", cancellationToken);
        _ = response.EnsureSuccessStatusCode();

        List<KavitaLibrary> libraries = await response.Content.ReadFromJsonAsync<List<KavitaLibrary>>(cancellationToken: cancellationToken)
                        ?? [];

        return libraries;
    }

    public async Task UpdateAllCollectionsAsync(CancellationToken cancellationToken)
    {
        KavitaSettings? settings = _dbContext.UserPreferences.Include(p => p.KavitaSettings).Query()
            .FirstOrDefault()?.KavitaSettings;
        if (settings == null)
        {
            return;
        }

        using HttpClient client = CreateClient(settings);
        await AttachAuthenticationAsync(client, settings, cancellationToken);

        using HttpResponseMessage response = await client.PostAsync("/api/Library/scan-all", null, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
    }
    public async Task<bool> TestConnection(KavitaSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient client = CreateClient(settings);
            await AttachAuthenticationAsync(client, settings, cancellationToken);

            // simple ping: GET /api/Library
            using HttpResponseMessage response = await client.GetAsync("/api/Library", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kavita connection test failed");
            return false;
        }
    }
    private static async Task AttachAuthenticationAsync(HttpClient client, KavitaSettings settings, CancellationToken cancellationToken)
    {
        if (settings.IsApiKey())
        {
            // Kavita API key: use plugin auth endpoint
            Uri uri = new(settings.ServiceUri, $"/api/Plugin/authenticate?apiKey={settings.ApiKey}&pluginName=KamiYomu");
            using HttpResponseMessage response = await client.PostAsync(uri, null, cancellationToken);
            _ = response.EnsureSuccessStatusCode();

            LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(login?.Token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", login.Token);
            }
        }
        else
        {
            // Basic username/password login
            var loginPayload = new
            {
                settings.Username,
                settings.Password
            };

            HttpResponseMessage response = await client.PostAsJsonAsync("/api/Account/login", loginPayload, cancellationToken);
            _ = response.EnsureSuccessStatusCode();

            LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(login?.Token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", login.Token);
            }
        }
    }

    private sealed record LoginResponse(string Token);
}
