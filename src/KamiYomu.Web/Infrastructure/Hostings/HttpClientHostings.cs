using System.Net;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.HttpHandlers;

using Microsoft.Extensions.Options;

using Polly;
using Polly.Extensions.Http;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Hostings;

/// <summary>
/// Hosting extensions for configuring HttpClient instances with retry and timeout policies, as well as custom user-agent headers.
/// </summary>
public static class HttpClientHostings
{
    /// <summary>
    /// Adds and configures HttpClient instances with retry and timeout policies, as well as custom user-agent headers.
    /// </summary>
    public static void AddHttpClientHostings(this WebApplicationBuilder builder)
    {
        AddWorkerHttpClient(builder.Services);

        AddIntegrationHttpClient(builder.Services);
    }

    private static void AddWorkerHttpClient(IServiceCollection services)
    {
        Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
         .HandleTransientHttpError()
         .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        Polly.Timeout.AsyncTimeoutPolicy<HttpResponseMessage> timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(AppOptions.Defaults.Worker.HttpTimeOutInSeconds);

        _ = services.AddHttpClient(Defaults.Worker.HttpClientApp, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
        })
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy);


        AddHttpHandlers(services);
    }

    private static void AddHttpHandlers(IServiceCollection services)
    {
        _ = services.AddSingleton(sp =>
        {
            IOptions<CloudflareSolverOptions> options = sp.GetRequiredService<IOptions<CloudflareSolverOptions>>();
            HttpClientHandler innerHandler = new()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            return new CloudflareBypassHandler(innerHandler, options);
        });

        _ = services.AddSingleton(sp =>
        {
            IOptions<ChromiumOptions> options = sp.GetRequiredService<IOptions<ChromiumOptions>>();
            HttpClientHandler innerHandler = new()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            return new ChromiumHandler(innerHandler, options);
        });

        _ = services.AddSingleton(sp =>
        {
            CloudflareBypassHandler cf = sp.GetRequiredService<CloudflareBypassHandler>();
            ChromiumHandler chromium = sp.GetRequiredService<ChromiumHandler>();

            return new SmartCrawlerHandler(
                new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                },
                cf,
                chromium);
        });

    }

    private static void AddIntegrationHttpClient(IServiceCollection services)
    {
        Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
         .HandleTransientHttpError()
         .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        Polly.Timeout.AsyncTimeoutPolicy<HttpResponseMessage> timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(AppOptions.Defaults.Worker.HttpTimeOutInSeconds);

        _ = services.AddHttpClient(Integrations.HttpClientApp, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
        })
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy)
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };
            });
    }
}
