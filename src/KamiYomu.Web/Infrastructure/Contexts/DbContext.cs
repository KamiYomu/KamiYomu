using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Addons;

using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts;

public class DbContext(string fileName, bool isReadOnly = false) : IDisposable
{
    private bool _disposed = false;
    private readonly Lazy<LiteDatabase> _raw = new(() =>
        new LiteDatabase(new ConnectionString
        {
            Filename = fileName,
            Connection = ConnectionType.Shared,
            ReadOnly = isReadOnly
        }),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    public LiteDatabase Raw => _raw.Value;
    public ILiteCollection<CrawlerAgent> CrawlerAgents => Raw.GetCollection<CrawlerAgent>("agent_crawlers");
    public ILiteCollection<Library> Libraries => Raw.GetCollection<Library>("libraries");
    public ILiteCollection<UserPreference> UserPreferences => Raw.GetCollection<UserPreference>("user_preferences");
    public ILiteCollection<NugetSource> NugetSources => Raw.GetCollection<NugetSource>("nuget_sources");
    public ILiteStorage<Guid> CrawlerAgentFileStorage => Raw.GetStorage<Guid>("_agent_crawler_file_storage", "_packages");



    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Raw?.Dispose();
        }
        _disposed = true;
    }

    ~DbContext()
    {
        Dispose(false);
    }
}
