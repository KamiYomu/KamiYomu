using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Storage;

namespace KamiYomu.Web.Infrastructure.Hostings;
/// <summary>
/// OptionsHostings provides extension methods for configuring application options and settings in the dependency injection container.
/// </summary>
public static class OptionsHostings
{

    public static void AddOptionsHostings(this WebApplicationBuilder builder)
    {
        _ = builder.Services.Configure<StartupOptions>(builder.Configuration.GetSection("StartupOptions"));
        _ = builder.Services.Configure<BasicAuthOptions>(builder.Configuration.GetSection("BasicAuth"));
        _ = builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
        _ = builder.Services.Configure<SpecialFolderOptions>(builder.Configuration.GetSection("SpecialFolders"));
        _ = builder.Services.Configure<ChromiumOptions>(builder.Configuration.GetSection("Chromium"));
        _ = builder.Services.PostConfigure<SpecialFolderOptions>(opts =>
        {
            opts.MangaDir = FileNameHelper.NormalizeSystemPath(opts.MangaDir);
            opts.AgentsDir = FileNameHelper.NormalizeSystemPath(opts.AgentsDir);
            opts.DbDir = FileNameHelper.NormalizeSystemPath(opts.DbDir);
            opts.LogDir = FileNameHelper.NormalizeSystemPath(opts.LogDir);

            _ = Directory.CreateDirectory(opts.LogDir);
            _ = Directory.CreateDirectory(opts.DbDir);
            _ = Directory.CreateDirectory(opts.MangaDir);
            _ = Directory.CreateDirectory(opts.AgentsDir);
        });
    }
}
