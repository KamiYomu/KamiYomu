using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Addons;
using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts;

public class DbContext : IDisposable
{
    private bool _disposed = false;
    private readonly LiteDatabase _database;
    public DbContext(string connectionString) {
        _database = new(connectionString);
    }

    public ILiteCollection<CrawlerAgent> CrawlerAgents => _database.GetCollection<CrawlerAgent>("agent_crawlers");
    public ILiteCollection<Library> Libraries => _database.GetCollection<Library>("libraries");
    public ILiteCollection<UserPreference> UserPreferences => _database.GetCollection<UserPreference>("user_preferences");
    public ILiteCollection<NugetSource> NugetSources => _database.GetCollection<NugetSource>("nuget_sources");
    public ILiteStorage<Guid> CrawlerAgentFileStorage => _database.GetStorage<Guid>("_agent_crawler_file_storage", "_packages");
    public LiteDatabase Raw => _database;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _database?.Dispose();
        }
        _disposed = true;
    }

    ~DbContext()
    {
        Dispose(false);
    }
}
