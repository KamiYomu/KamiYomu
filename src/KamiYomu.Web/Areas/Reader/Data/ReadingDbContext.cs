using KamiYomu.Web.Areas.Reader.Models;

using LiteDB;

namespace KamiYomu.Web.Areas.Reader.Data;

public class ReadingDbContext(string fileName, bool isReadOnly) : IDisposable
{
    private bool _disposed = false;
    public LiteDatabase Raw
    {
        get
        {
            // 1. Ensure the directory exists (LiteDB won't create folders)
            string? directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            // 2. Check if we need to force ReadOnly to false for the first run
            // If the file doesn't exist, ReadOnly MUST be false to create it.
            bool effectiveReadOnly = isReadOnly;
            if (!File.Exists(fileName))
            {
                effectiveReadOnly = false;
            }

            // 3. Initialize LiteDB
            // If the file is missing and effectiveReadOnly is false, LiteDB creates it.
            return new LiteDatabase(new ConnectionString
            {
                Filename = fileName,
                Connection = ConnectionType.Shared,
                ReadOnly = effectiveReadOnly
            });
        }
    }

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
