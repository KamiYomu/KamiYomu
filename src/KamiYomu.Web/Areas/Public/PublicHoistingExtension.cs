using System.Net;
using System.Text.Json;

using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using KamiYomu.Web.Areas.Public.Middlewares;
using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Areas.Public.Options;

using Microsoft.AspNetCore.Http.Timeouts;

namespace KamiYomu.Web.Areas.Public;

public static class PublicHoistingExtension
{
    public static IServiceCollection AddPublicArea(this IServiceCollection services)
    {
        _ = services.AddControllers();

        _ = services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        _ = services.AddSwaggerGen(c => c.EnableAnnotations());
        _ = services.ConfigureOptions<ConfigureSwaggerOptions>();


        _ = services.AddRequestTimeouts(options =>
        {
            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(30),
                TimeoutStatusCode = StatusCodes.Status408RequestTimeout
            };

            _ = options.AddPolicy("CrawlerAgentSearchPolicy", new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromMinutes(5),
                TimeoutStatusCode = StatusCodes.Status408RequestTimeout,
                WriteTimeoutResponse = async (context) =>
                {
                    context.Response.ContentType = "application/json";

                    PublicApiErrorResponse errorResponse = new()
                    {
                        Error = "Request Timeout",
                        Message = "The server took too long to respond to this request.",
                        TraceId = context.TraceIdentifier,
                        Timestamp = DateTime.UtcNow
                    };

                    string json = JsonSerializer.Serialize(errorResponse);
                    await context.Response.WriteAsync(json);
                }
            });
        });

        return services;
    }


    public static IApplicationBuilder UsePublicArea(this IApplicationBuilder app)
    {
        _ = app.UseRequestTimeouts();
        _ = app.UseSwagger();
        _ = app.UseSwaggerUI(options =>
        {
            IApiVersionDescriptionProvider provider = app.ApplicationServices
                .GetRequiredService<IApiVersionDescriptionProvider>();

            foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"KamiYomu {description.GroupName.ToUpperInvariant()}"
                );
            }

            options.DefaultModelsExpandDepth(-1);
            options.RoutePrefix = "public/api/swagger";
        });

        _ = app.UseMiddleware<PublicApiExceptionMiddleware>();

        return app;
    }
}
