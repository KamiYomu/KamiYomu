using KamiYomu.Web.Areas.Reader;
using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Repositories;
using KamiYomu.Web.Areas.Reader.Repositories.Interfaces;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Tests.Areas.Reader;

public class ReaderHoistingExtensionTests
{
    [Fact]
    public void AddReaderArea_RegistersReadingDbContextAndChapterProgressRepository()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReadingDb"] = ":memory:"
            })
            .Build();

        IServiceCollection result = services.AddReaderArea(configuration);

        Assert.Same(services, result);

        Assert.Contains(services, sd => sd.ServiceType == typeof(ReadingDbContext));
        Assert.Contains(services, sd => sd.ServiceKey is not null
            && sd.ServiceKey.Equals(ServiceLocator.ReadOnlyReadingDbContext)
            && sd.ServiceType == typeof(ReadingDbContext));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IChapterProgressRepository)
            && sd.ImplementationType == typeof(ChapterProgressRepository));

        ServiceProvider provider = services.BuildServiceProvider();

        ReadingDbContext readingDbContext = provider.GetRequiredService<ReadingDbContext>();
        Assert.NotNull(readingDbContext);

        ReadingDbContext readOnlyReadingDbContext = provider.GetRequiredKeyedService<ReadingDbContext>(ServiceLocator.ReadOnlyReadingDbContext);
        Assert.NotNull(readOnlyReadingDbContext);
    }
}
