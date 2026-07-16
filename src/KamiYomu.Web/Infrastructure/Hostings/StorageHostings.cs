using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Storage;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Infrastructure.Hostings;

/// <summary>
/// StorageHostings provides extension methods for configuring storage-related services in the dependency injection container. It allows for the registration of database contexts and caching mechanisms used in the application.
/// </summary>
public static class StorageHostings
{
    /// <summary>
    /// Add storage-related services to the dependency injection container. This includes registering database contexts for both agent and image databases, as well as a caching context. The method also configures read-only database contexts for scenarios where read-only access is required.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance used to configure services.</param>
    public static void AddStorageHostings(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddSingleton<CacheContext>();
        _ = builder.Services.AddScoped(_ => new DbContext(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("AgentDb")), false));
        _ = builder.Services.AddScoped(_ => new ImageDbContext(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("ImageDb")), false));
        _ = builder.Services.AddKeyedScoped(ServiceLocator.ReadOnlyDbContext, (sp, _) => new DbContext(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("AgentDb")), true));
        _ = builder.Services.AddKeyedScoped(ServiceLocator.ReadOnlyImageDbContext, (sp, _) => new ImageDbContext(FileNameHelper.NormalizeSystemPath(builder.Configuration.GetConnectionString("ImageDb")), true));

    }
}
