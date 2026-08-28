using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.HttpHandlers;
/// <summary>
/// SmartCrawlerHandler is an HTTP handler that intelligently routes requests through different strategies to bypass anti-bot protections. 
/// It first attempts to use a Cloudflare bypass handler, then falls back to a Chromium-based handler (using PuppeteerSharp), 
/// and finally defaults to the standard HttpClientHandler if both specialized handlers fail.
/// </summary>
/// <param name="logger">The ILogger instance.</param>
/// <param name="chromiumOptions"></param>
/// <param name="cloudFlareoOptions"></param>
/// <param name="cloudflare">The CloudflareBypassHandler instance.</param>
/// <param name="chromium">The ChromiumHandler instance.</param>
public class SmartCrawlerHandler(
    ILogger<SmartCrawlerHandler> logger,
    IOptions<ChromiumOptions> chromiumOptions,
    IOptions<CloudflareSolverOptions> cloudFlareoOptions,
    CloudflareBypassHandler cloudflare,
    ChromiumHandler chromium) : DelegatingHandler
{
    ///<inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {

        if (cloudFlareoOptions.Value.Enabled)
        {
            logger.LogDebug("SmartCrawlerHandler: 1. Try Cloudflare bypass first");
            HttpResponseMessage? cfResponse = await cloudflare.TrySendAsync(request, cancellationToken);
            if (cfResponse != null)
            {
                logger.LogDebug("SmartCrawlerHandler: FlareSolverr request is being processed...");
                return cfResponse;
            }
        }

        if (chromiumOptions.Value.Enabled)
        {
            logger.LogDebug("SmartCrawlerHandler: 2. Try Chromium (PuppeteerSharp)");
            HttpResponseMessage? chromiumResponse = await chromium.TrySendAsync(request, cancellationToken);
            if (chromiumResponse != null)
            {
                logger.LogDebug("SmartCrawlerHandler: Chromium request is being processed...");
                return chromiumResponse;
            }
        }


        logger.LogDebug("SmartCrawlerHandler: Fallback to default HttpClientHandler");
        return await base.SendAsync(request, cancellationToken);
    }
}
