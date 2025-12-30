using KamiYomu.Web.Entities;

using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts;

public class LibraryDbContext : IDisposable
{
    private bool _disposed = false;
    private readonly Guid _libraryId;

    public LibraryDbContext(Guid libraryId, bool readOnly = false)
    {
        _libraryId = libraryId;
        Raw = new(new ConnectionString
        {
            Filename = DatabaseFilePath(),
            Connection = ConnectionType.Shared,
            ReadOnly = readOnly
        });
    }
    public ILiteCollection<ChapterDownloadRecord> ChapterDownloadRecords => Raw.GetCollection<ChapterDownloadRecord>("chapter_download_records");
    public ILiteCollection<MangaDownloadRecord> MangaDownloadRecords => Raw.GetCollection<MangaDownloadRecord>("manga_download_records");
    public LiteDatabase Raw { get; }

    public string DatabaseFilePath()
    {
        return $"/db/lib{_libraryId}.db";
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
