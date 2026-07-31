using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.HttpHandlers;

using Microsoft.Extensions.DependencyInjection;
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

        _ = services.AddHttpClient(AppOptions.Defaults.Worker.HttpClientApp, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentSettings.HttpUserAgent);
        })
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy);


        AddHttpHandlers(services);
    }

    private static void AddHttpHandlers(IServiceCollection services)
    {
        services.AddSingleton<CloudflareBypassHandler>(sp =>
        {
            IOptions<CloudflareSolverOptions> cloudFlareOptions = sp.GetRequiredService<IOptions<CloudflareSolverOptions>>();
            HttpClientHandler innerHandler = new()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            return new CloudflareBypassHandler(innerHandler, cloudFlareOptions);
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
