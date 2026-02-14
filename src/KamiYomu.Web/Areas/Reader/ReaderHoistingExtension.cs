using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Repositories;
using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Reader;

public static class ReaderHoistingExtension
{
    public static IServiceCollection AddReaderArea(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.AddScoped(_ => new ReadingDbContext(configuration.GetConnectionString("ReadingDb"), false));
        _ = services.AddKeyedScoped(ServiceLocator.ReadOnlyReadingDbContext, (sp, _) => new ReadingDbContext(configuration.GetConnectionString("ReadingDb"), true));

        _ = services.AddTransient<IChapterProgressRepository, ChapterProgressRepository>();
        return services;
    }
}
