using System.Net.Http.Headers;

using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

using PuppeteerSharp;

namespace KamiYomu.Web.Infrastructure.HttpHandlers;

/// <summary>
/// HTTP handler that uses Chromium to render pages via PuppeteerSharp.
/// Manages browser lifecycle with proper disposal of resources.
/// </summary>
/// <param name="logger">The ILogger instance.</param>
/// <param name="options">Configuration options for Chromium behavior</param>
public sealed class ChromiumHandler(ILogger<ChromiumHandler> logger, IOptions<ChromiumOptions> options) : DelegatingHandler, IAsyncDisposable
{
    private readonly ChromiumOptions _options = options.Value;

    // Shared browser instance
    private static IBrowser? _browser;
    private static readonly SemaphoreSlim _browserInitLock = new(1, 1);
    private static bool _disposed = false;
    ///<inheritdoc/>
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
            logger.LogDebug("Chromium: Using existing Chromium browser instance.");
            return _browser;
        }

        await _browserInitLock.WaitAsync(ct);
        try
        {
            if (_browser != null && !_browser.IsClosed)
            {
                logger.LogDebug("Chromium: Using existing Chromium browser instance.");
                return _browser;
            }

            logger.LogDebug("Chromium: Ensure Chromium is downloaded.");
            BrowserFetcher fetcher = new(new BrowserFetcherOptions
            {
                Path = _options.GetExecutablePath()
            });

            if (File.Exists(_options.GetExecutablePath()))
            {
                logger.LogDebug("Chromium: executable already exists at {Path}. Skipping download.", fetcher.GetExecutablePath(_options.GetExecutablePath()));
            }
            else
            {
                logger.LogDebug("Chromium: executable not found. Downloading...");
                _ = await fetcher.DownloadAsync(BrowserTag.Stable);
            }

            // Launch Chromium
            logger.LogDebug("Chromium: Launching browser instance.");
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = _options.GetExecutablePath(),
                Args = _options.Arguments
            });

            return _browser;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chromium: Trying to launch chromium.");
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
        logger.LogDebug("Chromium: Create a new Page.");
        await using IPage page = await browser.NewPageAsync();
        request.Headers.IfNoneMatch.Clear();
        request.Headers.IfModifiedSince = null;
        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MaxAge = TimeSpan.Zero
        };
        logger.LogDebug("Chromium: Copy headers to chromium page.");
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
        logger.LogDebug("Chromium: Navigate to the URL requested: {url}", url);
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
        logger.LogDebug("Chromium: Building HttpResponseMessage from Chromium response.");
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
                logger.LogDebug("Chromium: Close chromium on dispose.");
                await _browser.CloseAsync();
            }

            _disposed = true;
        }
        finally
        {
            logger.LogDebug("Chromium: Release lock.");
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
                    logger.LogDebug("Chromium: Close chromium on dispose.");
                    _browser.CloseAsync().GetAwaiter().GetResult();
                }

                _disposed = true;
            }
            finally
            {
                logger.LogDebug("Chromium: Release lock.");
                _ = _browserInitLock.Release();
            }
        }

        base.Dispose(disposing);
    }
}
