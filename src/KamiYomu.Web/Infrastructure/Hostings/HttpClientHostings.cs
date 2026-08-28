using System.Net;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.HttpHandlers;

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
        AddHttpHandlers(services);

        Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
         .HandleTransientHttpError()
         .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        Polly.Timeout.AsyncTimeoutPolicy<HttpResponseMessage> timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(Defaults.Worker.HttpTimeOutInSeconds);

        _ = services.AddHttpClient(Defaults.Worker.WorkerHttpClient, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentMetadata.Values.MimicUserAgent);
        })
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy);

        _ = services.AddHttpClient(CrawlerAgentMetadata.Fields.ApplicationHttpClient, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentMetadata.Values.MimicUserAgent);
        }).AddHttpMessageHandler<SmartCrawlerHandler>()
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(timeoutPolicy);


    }

    private static void AddHttpHandlers(IServiceCollection services)
    {
        _ = services.AddTransient<CloudflareBypassHandler>();
        _ = services.AddTransient<ChromiumHandler>();
        _ = services.AddTransient<SmartCrawlerHandler>();
    }

    private static void AddIntegrationHttpClient(IServiceCollection services)
    {
        Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> retryPolicy = HttpPolicyExtensions
         .HandleTransientHttpError()
         .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        Polly.Timeout.AsyncTimeoutPolicy<HttpResponseMessage> timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(Defaults.Worker.HttpTimeOutInSeconds);

        _ = services.AddHttpClient(Integrations.HttpClientApp, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CrawlerAgentMetadata.Values.MimicUserAgent);
        })
            .AddPolicyHandler(retryPolicy)
            .AddPolicyHandler(timeoutPolicy)
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
            });
    }
}
