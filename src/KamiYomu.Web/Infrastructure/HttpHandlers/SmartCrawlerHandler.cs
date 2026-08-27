namespace KamiYomu.Web.Infrastructure.HttpHandlers;

public class SmartCrawlerHandler(
    CloudflareBypassHandler cloudflare,
    ChromiumHandler chromium) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        //
        // 1. Try Cloudflare bypass first
        //
        HttpResponseMessage? cfResponse = await cloudflare.TrySendAsync(request, cancellationToken);
        if (cfResponse != null)
        {
            return cfResponse;
        }

        //
        // 2. Try Chromium (PuppeteerSharp)
        //
        HttpResponseMessage? chromiumResponse = await chromium.TrySendAsync(request, cancellationToken);
        if (chromiumResponse != null)
        {
            return chromiumResponse;
        }

        //
        // 3. Fallback to default HttpClientHandler
        //
        return await base.SendAsync(request, cancellationToken);
    }
}
