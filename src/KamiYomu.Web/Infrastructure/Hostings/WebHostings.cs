using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Areas.Public;
using KamiYomu.Web.Areas.Reader;
using KamiYomu.Web.Entities;
using KamiYomu.Web.HealthCheckers;
using KamiYomu.Web.Hubs;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Middlewares;

using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

using MonkeyCache;
using MonkeyCache.LiteDB;

using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Hostings;

/// <summary>
/// WebHostings provides extension methods for configuring web hosting services and middleware in the dependency injection container. It includes settings for logging, response compression, localization, health checks, and other web-related configurations.
/// </summary>
public static class WebHostings
{
    /// <summary>
    /// Adds web hosting services and middleware configurations to the WebApplicationBuilder. This method sets up logging with Serilog, configures response compression, localization, health checks, and registers various services required for the web application to function properly.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder to configure.</param>
    public static void AddWebHostings(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddHttpContextAccessor();
        _ = builder.Services.AddSignalR();

        _ = builder.Services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        _ = builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<GzipCompressionProvider>();
        });

        _ = builder.Services.AddSingleton<IUserClockManager, UserClockManager>();
        _ = builder.Services.AddSingleton<ILockManager, LockManager>();

        // Health Checks
        _ = builder.Services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>(nameof(DatabaseHealthCheck), tags: ["storage"])
                .AddCheck<WorkerHealthCheck>(nameof(WorkerHealthCheck), tags: ["worker"])
                .AddCheck<CachingHealthCheck>(nameof(CachingHealthCheck), tags: ["storage"]);

        _ = builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

        _ = builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            CultureInfo[] supportedCultures =
            [
                    new CultureInfo("en-US"),
            new CultureInfo("pt-BR"),
            new CultureInfo("fr"),
            new CultureInfo("es"),
            new CultureInfo("nl")
            ];

            options.DefaultRequestCulture = new RequestCulture("en-US");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            options.FallBackToParentCultures = true;
            options.FallBackToParentUICultures = true;
        });

        LiteDbConfig.Configure();

        _ = builder.Services.AddRazorPages()
                        .AddJsonOptions(options =>
                        {
                            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                        })
                        .AddViewLocalization()
                        .AddDataAnnotationsLocalization();

        _ = builder.Services.AddReaderArea(builder.Configuration)
                            .AddPublicArea();

        AddQuestPDF();


    }

    private static void AddQuestPDF()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Assembly assembly = Assembly.GetExecutingAssembly();

        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-Black.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-BlackItalic.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-Bold.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-BoldItalic.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-Italic.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-Light.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-LightItalic.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-Regular.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-Thin.ttf");
        RegisterFont("KamiYomu.Web.Resources.Fonts.Lato.Lato-ThinItalic.ttf");

        RegisterFont("KamiYomu.Web.Resources.Fonts.Gloria_Hallelujah.GloriaHallelujah-Regular.ttf");

        _ = TextStyle.Default.FontFamily("Lato");

        void RegisterFont(string resourceName)
        {
            Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded font '{resourceName}' not found.");

            FontManager.RegisterFont(stream);
        }
    }

    public static void UseWebHostings(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            QuestPDF.Settings.EnableDebugging = true;
            _ = app.UseExceptionHandler("/Error");
        }

        DefaultLanguage(app);

        _ = app.UseResponseCompression();
        _ = app.UseStaticFiles();
        _ = app.UseRouting();
        _ = app.UseMiddleware<BasicAuthMiddleware>();
        _ = app.UsePublicArea();
        _ = app.MapControllers();
        _ = app.MapRazorPages();
        _ = app.UseMiddleware<ExceptionNotificationMiddleware>();
        _ = app.MapHub<NotificationHub>("/notificationHub");
        _ = app.MapHealthChecks("/healthz");
    }

    private static void DefaultLanguage(WebApplication app)
    {
        using IServiceScope appScoped = app.Services.CreateScope();
        SpecialFolderOptions specialFolderOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<SpecialFolderOptions>>().Value;
        StartupOptions startupOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
        IOptions<RequestLocalizationOptions> localizationOptions = appScoped.ServiceProvider.GetRequiredService<IOptions<RequestLocalizationOptions>>();

        Barrel.ApplicationId = nameof(KamiYomu);
        BarrelUtils.SetBaseCachePath(specialFolderOptions.DbDir);

        using DbContext dbcontext = appScoped.ServiceProvider.GetRequiredService<DbContext>();
        UserPreference userPreference = dbcontext.UserPreferences.FindOne(p => true);
        if (userPreference == null)
        {
            userPreference = new UserPreference(new CultureInfo(startupOptions.DefaultLanguage));
            _ = appScoped.ServiceProvider.GetService<DbContext>()!.UserPreferences.Insert(userPreference);
        }

        localizationOptions.Value.DefaultRequestCulture = new RequestCulture(userPreference!.GetCulture());

        _ = app.UseRequestLocalization(localizationOptions.Value);
    }
}
