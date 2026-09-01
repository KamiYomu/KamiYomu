using System.Net;
using System.Text.Json;

using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.HttpHandlers;
/// <summary>
/// CloudflareBypassHandler is an HttpMessageHandler that attempts to bypass Cloudflare's anti-bot protection by using FlareSolverr.
/// </summary>
/// <param name="logger">The ILogger instance.</param>
/// <param name="options">The CloudflareSolverOptions instance.</param>
public class CloudflareBypassHandler(ILogger<CloudflareBypassHandler> logger, IOptions<CloudflareSolverOptions> options)
    : DelegatingHandler
{
    ///<inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("FlareSolverr: First attempt, without flaresolverr");
        HttpRequestMessage send = new(request.Method, request.RequestUri);

        using HttpClient client = new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });

        try
        {
            HttpResponseMessage response = await client.SendAsync(send, cancellationToken);

            if (!await IsCloudflareBlock(response))
            {
                logger.LogDebug("FlareSolverr: Request not blocked. Returning response.");
                return response;
            }

            response.Dispose();
        }
        catch
        {
            send.Dispose();
            throw;
        }

        logger.LogDebug("FlareSolverr: Solve using FlareSolverr.");
        (string cookieHeader, string userAgent) = await SolveWithFlareSolverr(request.RequestUri!.ToString());

        logger.LogDebug("FlareSolverr: Retry with cookies + UA.");

        HttpRequestMessage retry = new(request.Method, request.RequestUri);

        try
        {
            retry.Headers.Add("User-Agent", userAgent);
            retry.Headers.Add("Cookie", cookieHeader);

            return await client.SendAsync(retry, cancellationToken);
        }
        catch
        {
            retry.Dispose();
            throw;
        }
        finally
        {
            send.Dispose();
        }
    }

    private async Task<bool> IsCloudflareBlock(HttpResponseMessage response)
    {
        logger.LogDebug("FlareSolverr: If the CloudflareSolverr is not enabled, we won't attempt to bypass Cloudflare.: {enabled}", options.Value.Enabled);
        if (!options.Value.Enabled)
        {
            return false;
        }

        if ((int)response.StatusCode is 403 or 503)
        {
            logger.LogDebug("FlareSolverr: Response blocked: status code {StatusCode}", response.StatusCode);
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
        logger.LogDebug("FlareSolverr: Start solving...");
        var payload = new
        {
            cmd = "request.get",
            url,
            maxTimeout = options.Value.MaxTimeout
        };

        using HttpClient client = new();
        HttpResponseMessage response = await client.PostAsJsonAsync($"{options.Value.Uri}/v1", payload);
        string json = await response.Content.ReadAsStringAsync();

        logger.LogDebug("FlareSolverr: Extract only what we need using JsonDocument (no POCO classes)");
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement solution = doc.RootElement.GetProperty("solution");

        string? userAgent = solution.GetProperty("userAgent").GetString();

        IEnumerable<string> cookies = solution.GetProperty("cookies")
            .EnumerateArray()
            .Select(c => $"{c.GetProperty("name").GetString()}={c.GetProperty("value").GetString()}");

        string cookieHeader = string.Join("; ", cookies);

        return (cookieHeader, userAgent);
    }

    public async Task<HttpResponseMessage?> TrySendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await SendAsync(request, ct);

            logger.LogDebug("FlareSolverr: If Cloudflare solved -> return response");
            if (!await IsCloudflareBlock(response))
            {
                return response;
            }

            logger.LogDebug("FlareSolverr: Cloudflare still blocking -> escalate");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FlareSolverr: Fail -> escalate");
            return null;
        }
    }
}

