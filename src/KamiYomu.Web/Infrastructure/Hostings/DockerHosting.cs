using KamiYomu.Web.Infrastructure.Storage;

using Serilog;

namespace KamiYomu.Web.Infrastructure.Hostings;
/// <summary>
/// 
/// </summary>
public static class DockerHosting
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    public static void AddDockerHostings(this WebApplicationBuilder builder)
    {
        if (!FileNameHelper.IsRunningInDocker())
        {
            return;
        }

        Log.Logger = new LoggerConfiguration()
                        .ReadFrom.Configuration(builder.Configuration)
                        .CreateLogger();

        _ = builder.Host.UseSerilog((context, services, configuration) =>
               configuration
                   .ReadFrom.Configuration(context.Configuration)
                   .ReadFrom.Services(services)
                   .Enrich.FromLogContext()
           );
    }
}
