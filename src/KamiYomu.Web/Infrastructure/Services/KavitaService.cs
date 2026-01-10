using System.Net.Http.Headers;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Kavita;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using PuppeteerSharp;

namespace KamiYomu.Web.Infrastructure.Services;

public class KavitaService(ILogger<KavitaService> logger, DbContext dbContext, IHttpClientFactory httpClientFactory) : IKavitaService
{
    private readonly Lazy<HttpClient> _httpClient = new(() =>
    {
        HttpClient client = httpClientFactory.CreateClient(Defaults.Integrations.HttpClientApp);

        UserPreference preferences = dbContext.UserPreferences.Query().FirstOrDefault();

        if (preferences?.KavitaSettings?.ServiceUri != null)
        {
            client.BaseAddress = preferences.KavitaSettings.ServiceUri;
        }

        return client;
    });

    private HttpClient Client => _httpClient.Value;

    public async Task<IReadOnlyList<KavitaLibrary>> LoadAllCollectionsAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/Library");

        using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
        _ = response.EnsureSuccessStatusCode();

        List<KavitaLibrary> libraries = await response.Content.ReadFromJsonAsync<List<KavitaLibrary>>(cancellationToken: cancellationToken)
                         ?? [];

        return libraries;
    }

    public async Task<bool> TryConnectToKavita(KavitaSettings kavitaSettings, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = GetAuthenticationRequestMessage(kavitaSettings);

        try
        {
            using HttpClient httpClient = new();
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error connecting to Kavita: {Message}", ex.Message);
            return false;
        }
    }


    public async Task UpdateAllCollectionsAsync(CancellationToken cancellationToken)
    {

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/Library/scan-all");

        using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
        _ = response.EnsureSuccessStatusCode();
    }


    public static HttpRequestMessage GetAuthenticationRequestMessage(KavitaSettings kavitaSettings)
    {
        HttpRequestMessage request;

        if (kavitaSettings.IsApiKey())
        {
            Uri loginUri = new(kavitaSettings.ServiceUri, $"/api/Plugin/authenticate?apiKey={kavitaSettings.ApiKey}&pluginName=KamiYomu");

            request = new(HttpMethod.Post, loginUri);
        }
        else
        {
            Uri loginUri = new(kavitaSettings.ServiceUri, "/api/Account/login");

            request = new(HttpMethod.Post, loginUri)
            {
                Content = JsonContent.Create(new
                {
                    kavitaSettings.Username,
                    kavitaSettings.Password
                })
            };
        }
        return request;
    }
}

public class KavitaAuthHandler(DbContext dbContext) : DelegatingHandler
{
    private string? _jwtToken;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Ensure token is set
        if (string.IsNullOrWhiteSpace(_jwtToken))
        {
            await AuthenticateAsync(cancellationToken);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // Retry once if 401 Unauthorized
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _jwtToken = null; // invalidate token
            await AuthenticateAsync(cancellationToken);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            response.Dispose(); // discard old response
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_jwtToken))
            {
                return;
            }

            UserPreference preference = dbContext.UserPreferences.Query().FirstOrDefault();
            if (preference?.KavitaSettings?.Enabled != true)
            {
                throw new InvalidOperationException("Kavita integration disabled");
            }

            using HttpClient httpClient = new();

            using HttpRequestMessage request = KavitaService.GetAuthenticationRequestMessage(preference?.KavitaSettings);

            HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

            _ = response.EnsureSuccessStatusCode();

            LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(login?.Token))
            {
                throw new InvalidOperationException("Kavita authentication failed");
            }

            _jwtToken = login.Token;
        }
        finally
        {
            _ = _authLock.Release();
        }
    }



    private sealed record LoginResponse(string Token);
}
