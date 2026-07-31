using System.Text.Json;

using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.HttpHandlers;
/// <summary>
/// CloudflareBypassHandler is an HttpMessageHandler that attempts to bypass Cloudflare's anti-bot protection by using FlareSolverr.
/// </summary>
/// <param name="innerHandler">The inner HttpMessageHandler to delegate requests to.</param>
/// <param name="options">The CloudflareSolverOptions instance.</param>
public class CloudflareBypassHandler(HttpMessageHandler innerHandler, IOptions<CloudflareSolverOptions> options) : DelegatingHandler(innerHandler)
{
    /// <summary>
    /// Sends an HTTP request and attempts to bypass Cloudflare's anti-bot protection if necessary.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The HTTP response message.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // First attempt
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (!await IsCloudflareBlock(response))
        {
            return response;
        }

        // Solve using FlareSolverr
        (string cookieHeader, string userAgent) = await SolveWithFlareSolverr(request.RequestUri.ToString());

        // Retry with cookies + UA
        HttpRequestMessage retry = new(request.Method, request.RequestUri);

        retry.Headers.Add("User-Agent", userAgent);
        retry.Headers.Add("Cookie", cookieHeader);

        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task<bool> IsCloudflareBlock(HttpResponseMessage response)
    {
        // If the CloudflareSolverr is not enabled, we won't attempt to bypass Cloudflare.
        if (!options.Value.Enabled)
        {
            return false;
        }

        if ((int)response.StatusCode is 403 or 503)
        {
            return true;
        }

        string html = await response.Content.ReadAsStringAsync();

        bool isCloudflareChallenge =
            response.Headers.Contains("cf-ray") &&
            (
                html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("challenge-platform", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("/cdn-cgi/challenge-platform/", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("cf_chl_", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("__cf_chl_", StringComparison.OrdinalIgnoreCase)
            );

        return isCloudflareChallenge;
    }

    private async Task<(string cookieHeader, string userAgent)> SolveWithFlareSolverr(string url)
    {
        var payload = new
        {
            cmd = "request.get",
            url,
            maxTimeout = options.Value.MaxTimeout
        };

        using HttpClient client = new();
        HttpResponseMessage response = await client.PostAsJsonAsync($"{options.Value.Uri}/v1", payload);
        string json = await response.Content.ReadAsStringAsync();

        // Extract only what we need using JsonDocument (no POCO classes)
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement solution = doc.RootElement.GetProperty("solution");

        string? userAgent = solution.GetProperty("userAgent").GetString();

        IEnumerable<string> cookies = solution.GetProperty("cookies")
            .EnumerateArray()
            .Select(c => $"{c.GetProperty("name").GetString()}={c.GetProperty("value").GetString()}");

        string cookieHeader = string.Join("; ", cookies);

        return (cookieHeader, userAgent);
    }
}

