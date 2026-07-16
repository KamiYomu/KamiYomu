using KamiYomu.Web.Infrastructure.Repositories;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

namespace KamiYomu.Web.Infrastructure.Hostings;

/// <summary>
/// repositoriesHostings provides extension methods for registering repository implementations with the dependency injection container. It allows for the addition of various repositories related to crawler agents and Hangfire operations.
/// </summary>
public static class RepositoriesHostings
{
    /// <summary>
    /// Adds repository implementations to the dependency injection container. This method registers the CrawlerAgentRepository and HangfireRepository, which can be used for managing crawler agents and Hangfire operations, respectively.
    /// </summary>
    /// <param name="builder"></param>
    public static void AddRepositoriesHostings(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddTransient<ICrawlerAgentRepository, CrawlerAgentRepository>();
        _ = builder.Services.AddTransient<IHangfireRepository, HangfireRepository>();
    }
}
