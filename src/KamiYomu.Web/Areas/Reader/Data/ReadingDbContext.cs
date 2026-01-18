using KamiYomu.Web.Areas.Reader.Models;

using LiteDB;

namespace KamiYomu.Web.Areas.Reader.Data;

public class ReadingDbContext(string fileName, bool isReadOnly) : IDisposable
{
    private bool _disposed = false;
    public LiteDatabase Raw => new(new ConnectionString
    {
        Filename = fileName,
        Connection = ConnectionType.Shared,
        ReadOnly = isReadOnly
    });

    public ILiteCollection<ChapterProgress> ChapterProgress => Raw.GetCollection<ChapterProgress>("chapter_progress");

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

    ~ReadingDbContext()
    {
        Dispose(false);
    }
}
