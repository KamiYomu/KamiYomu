using KamiYomu.Web.Entities;

using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts;

public class LibraryDbContext(Guid libraryId, bool isReadOnly = false) : IDisposable
{
    private bool _disposed = false;
    private ILiteDatabase _raw;

    public ILiteCollection<ChapterDownloadRecord> ChapterDownloadRecords => Raw.GetCollection<ChapterDownloadRecord>("chapter_download_records");
    public ILiteCollection<MangaDownloadRecord> MangaDownloadRecords => Raw.GetCollection<MangaDownloadRecord>("manga_download_records");
    public ILiteDatabase Raw
    {
        get
        {
            if (_raw != null)
            {
                return _raw;
            }

            string fileName = DatabaseFilePath();
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
            _raw = fileName.StartsWith(":") ? new LiteDatabase(fileName) : new LiteDatabase(new ConnectionString
            {
                Filename = fileName,
                Connection = ConnectionType.Shared,
                ReadOnly = effectiveReadOnly
            });

            return _raw;
        }
    }

    public string DatabaseFilePath()
    {
        return $"/db/lib{libraryId}.db";
    }

    public void DropDatabase()
    {
        Raw.Dispose();
        _disposed = true;
        if (File.Exists(DatabaseFilePath()))
        {
            File.Delete(DatabaseFilePath());
        }
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
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~LibraryDbContext()
    {
        Dispose(false);
    }
}
