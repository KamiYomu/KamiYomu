using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using KamiYomu.Web.Areas.Public.Options;

namespace KamiYomu.Web.Areas.Public;

public static class PublicHoistingExtension
{
    public static IServiceCollection AddPublicApi(this IServiceCollection services)
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

        return services;
    }


    public static IApplicationBuilder UsePublicApi(this IApplicationBuilder app)
    {

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

        return app;
    }
}
