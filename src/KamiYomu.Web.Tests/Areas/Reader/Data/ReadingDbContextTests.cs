using KamiYomu.Web.Areas.Reader.Data;
using KamiYomu.Web.Areas.Reader.Models;

namespace KamiYomu.Web.Tests.Areas.Reader.Data;

public class ReadingDbContextTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "KamiYomu.Tests.ReadingDbContext", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Raw_WhenDirectoryMissing_CreatesDirectoryAndDatabaseFile()
    {
        string dbPath = Path.Combine(_rootPath, "nested", "reading.db");

        using ReadingDbContext context = new(dbPath, false);

        _ = context.Raw;

        Assert.True(Directory.Exists(Path.GetDirectoryName(dbPath)));
    }

    [Fact]
    public void ChapterProgress_AllowsInsertAndQuery()
    {
        string dbPath = Path.Combine(_rootPath, "reading.db");
        using ReadingDbContext context = new(dbPath, false);

        ChapterProgress progress = new(Guid.NewGuid(), Guid.NewGuid(), 1);
        _ = context.ChapterProgress.Insert(progress);

        List<ChapterProgress> results = context.ChapterProgress.Query().ToList();

        _ = Assert.Single(results);
    }

    [Fact]
    public void Raw_WhenFileDoesNotExistAndReadOnlyRequested_StillAllowsWrites()
    {
        string dbPath = Path.Combine(_rootPath, "readonly.db");
        using ReadingDbContext context = new(dbPath, true);

        // LiteDB only materializes the file on disk once a collection is actually used;
        // opening Raw alone does not touch the filesystem.
        _ = context.ChapterProgress.Insert(new ChapterProgress(Guid.NewGuid(), Guid.NewGuid(), 1));

        Assert.True(File.Exists(dbPath));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimesWithoutThrowing()
    {
        string dbPath = Path.Combine(_rootPath, "dispose.db");
        ReadingDbContext context = new(dbPath, false);
        _ = context.Raw;

        context.Dispose();
        Exception? exception = Record.Exception(context.Dispose);

        Assert.Null(exception);
    }
}
