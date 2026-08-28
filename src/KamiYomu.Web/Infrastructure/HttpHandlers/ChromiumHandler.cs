using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

using PuppeteerSharp;

namespace KamiYomu.Web.Infrastructure.HttpHandlers;

/// <summary>
/// HTTP handler that uses Chromium to render pages via PuppeteerSharp.
/// Manages browser lifecycle with proper disposal of resources.
/// </summary>
/// <param name="options">Configuration options for Chromium behavior</param>
public sealed class ChromiumHandler(IOptions<ChromiumOptions> options) : DelegatingHandler, IAsyncDisposable
{
    private readonly ChromiumOptions _options = options.Value;

    // Shared browser instance
    private static IBrowser? _browser;
    private static readonly SemaphoreSlim _browserInitLock = new(1, 1);
    private static bool _disposed = false;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Try Chromium first
        try
        {
            IBrowser? browser = await GetOrCreateBrowserAsync(cancellationToken);
            return browser is null
                ? await base.SendAsync(request, cancellationToken)
                : await HandleWithChromiumAsync(browser, request, cancellationToken);
        }
        catch
        {
            // Fallback to default handler
            return await base.SendAsync(request, cancellationToken);
        }
    }

    private async Task<IBrowser?> GetOrCreateBrowserAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        if (_browser != null && !_browser.IsClosed)
        {
            return _browser;
        }

        await _browserInitLock.WaitAsync(ct);
        try
        {
            if (_browser != null && !_browser.IsClosed)
            {
                return _browser;
            }

            // Ensure Chromium is downloaded
            BrowserFetcher fetcher = new(new BrowserFetcherOptions
            {
                Path = _options.GetExecutablePath()
            });

            _ = await fetcher.DownloadAsync(BrowserTag.Stable);

            // Launch Chromium
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = fetcher.GetExecutablePath(_options.GetExecutablePath()),
                Args = _options.Arguments
            });

            return _browser;
        }
        catch
        {
            return null;
        }
        finally
        {
            _ = _browserInitLock.Release();
        }
    }

    private async Task<HttpResponseMessage> HandleWithChromiumAsync(
        IBrowser browser,
        HttpRequestMessage request,
        CancellationToken ct)
    {
        await using IPage page = await browser.NewPageAsync();

        // Copy headers to Chromium
        if (request.Headers != null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
                {
                    [header.Key] = string.Join(",", header.Value)
                });
            }
        }

        string url = request.RequestUri!.ToString();

        // Navigate
        IResponse response = await page.GoToAsync(url, new NavigationOptions
        {
            Timeout = _options.RequestTimeout,
            WaitUntil =
            [
                WaitUntilNavigation.DOMContentLoaded,
                WaitUntilNavigation.Load,
                WaitUntilNavigation.Networkidle0
            ]
        });

        string content = await page.GetContentAsync();

        // Build HttpResponseMessage
        HttpResponseMessage httpResponse = new(response.Status)
        {
            Content = new StringContent(content)
        };

        // Copy Chromium response headers
        foreach (KeyValuePair<string, string> h in response.Headers)
        {
            _ = httpResponse.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        return httpResponse;
    }

    public async Task<HttpResponseMessage?> TrySendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        try
        {
            IBrowser? browser = await GetOrCreateBrowserAsync(ct);
            return browser == null ? null : await HandleWithChromiumAsync(browser, request, ct);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Disposes the browser and releases all associated resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _browserInitLock.WaitAsync();
        try
        {
            if (!_disposed && _browser != null && !_browser.IsClosed)
            {
                await _browser.CloseAsync();
            }

            _disposed = true;
        }
        finally
        {
            _ = _browserInitLock.Release();
            // await base.DisposeAsync();
        }
    }

    /// <summary>
    /// Synchronous disposal fallback for compatibility.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _browserInitLock.WaitAsync().GetAwaiter().GetResult();
            try
            {
                if (!_disposed && _browser != null && !_browser.IsClosed)
                {
                    _browser.CloseAsync().GetAwaiter().GetResult();
                }

                _disposed = true;
            }
            finally
            {
                _ = _browserInitLock.Release();
            }
        }

        base.Dispose(disposing);
    }
}
