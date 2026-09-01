using System.Net.Http.Headers;
using System.Threading;

using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

using PuppeteerSharp;

namespace KamiYomu.Web.Infrastructure.HttpHandlers;

/// <summary>
/// HTTP handler that uses Chromium to render pages via PuppeteerSharp.
/// Manages browser lifecycle with proper disposal of resources.
/// </summary>
/// <param name="logger">The ILogger instance.</param>
/// <param name="chromiumOptions">Configuration options for Chromium behavior</param>
public sealed class ChromiumHandler(ILogger<ChromiumHandler> logger, IOptions<ChromiumOptions> chromiumOptions, IOptions<WorkerOptions> workerOptions) : DelegatingHandler, IAsyncDisposable
{
    private readonly ChromiumOptions _options = chromiumOptions.Value;
    private readonly WorkerOptions _workerOptions = workerOptions.Value;

    // Shared browser instance
    private static IBrowser? _browser;
    private static readonly SemaphoreSlim _browserInitLock = new(1, 1);
    private static SemaphoreSlim? _maxNumberPageLock = null;
    private static bool _disposed = false;
    ///<inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            IBrowser? browser = await GetOrCreateBrowserAsync(cancellationToken);

            if (browser is null || _maxNumberPageLock is null)
            {
                return await base.SendAsync(request, cancellationToken);
            }
            else
            {
                return await HandleWithChromiumAsync(browser, request, cancellationToken);

            }
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

            // Launch Chromium
            logger.LogDebug("Chromium: Launching browser instance.");
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = _options.Arguments
            });

            int maxPage = (int)(_workerOptions.MaxConcurrentCrawlerInstances * 1.5);

            logger.LogDebug("Chromium: Setting up page initialization lock with maxPage: {maxPage}", maxPage);
            _maxNumberPageLock ??= new SemaphoreSlim(1, maxPage);

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
        CancellationToken cancellationToken)
    {
        if (_maxNumberPageLock is null)
        {
            throw new InvalidOperationException("Chromium: Page initialization lock is null. This should not happen.");
        }
        logger.LogDebug("Chromium: Wait for page available, Number of pages available: {numberPages}.", _maxNumberPageLock?.CurrentCount);
        await _maxNumberPageLock.WaitAsync(cancellationToken);
        try
        {
            logger.LogDebug("Chromium: Start the navigation, Number of pages available: {numberPages}.", _maxNumberPageLock?.CurrentCount);
            logger.LogDebug("Chromium: Create a new Page.");
            await using IPage page = await browser.NewPageAsync();
            try
            {
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
            catch
            {
                logger.LogError("Chromium: Error during page processing.");
                throw;
            }
        }
        finally
        {
            _ = _maxNumberPageLock?.Release();
            logger.LogDebug("Chromium: End of the navigation, Number of pages available: {numberPages}.", _maxNumberPageLock?.CurrentCount);
        }
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
                await _browser.DisposeAsync();
                _browser = null;
            }

            if (_maxNumberPageLock != null)
            {
                _maxNumberPageLock.Dispose();
                _maxNumberPageLock = null;
            }

            _disposed = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chromium: Error during async disposal.");
        }
        finally
        {
            _ = _browserInitLock.Release();
            logger.LogDebug("Chromium: Browser instance release lock.");
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
                    _browser.DisposeAsync().GetAwaiter().GetResult();
                    _browser = null;
                }

                if (_maxNumberPageLock != null)
                {
                    _maxNumberPageLock.Dispose();
                    _maxNumberPageLock = null;
                }

                _disposed = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Chromium: Error during synchronous disposal.");
            }
            finally
            {
                _ = _browserInitLock.Release();
                logger.LogDebug("Chromium: Browser instance release lock.");
            }
        }

        base.Dispose(disposing);
    }
}
