using KamiYomu.Web.Entities;

using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts;
/// <summary>
/// 
/// </summary>
/// <param name="libraryId"></param>
/// <param name="isReadOnly"></param>
public class LibraryDbContext(Guid libraryId, bool isReadOnly = false) : IDisposable
{
    private bool _disposed = false;
    private ILiteDatabase _raw;
    /// <summary>
    /// Gets the collection of chapter download records.
    /// </summary>
    public ILiteCollection<ChapterDownloadRecord> ChapterDownloadRecords => Raw.GetCollection<ChapterDownloadRecord>("chapter_download_records");
    /// <summary>
    /// Gets the collection of manga download records.
    /// </summary>
    public ILiteCollection<MangaDownloadRecord> MangaDownloadRecords => Raw.GetCollection<MangaDownloadRecord>("manga_download_records");
    /// <summary>
    /// Gets the underlying LiteDB database instance.
    /// </summary>
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
    /// <summary>
    /// Gets the file path for the database.
    /// </summary>
    /// <returns>The file path of the database.</returns>
    public string DatabaseFilePath()
    {
        return $"/db/lib{libraryId}.db";
    }
    /// <summary>
    /// Drops the database by disposing the current instance and deleting the database file.
    /// </summary>
    public void DropDatabase()
    {
        Raw.Dispose();
        _disposed = true;
        if (File.Exists(DatabaseFilePath()))
        {
            File.Delete(DatabaseFilePath());
        }
    }
    /// <summary>
    /// Disposes the current instance and releases all resources.
    /// </summary>
    /// <param name="disposing">Indicates whether the method is called from Dispose.</param>
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
    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    /// <summary>
    /// 
    /// </summary>
    ~LibraryDbContext()
    {
        Dispose(false);
    }
}
