using KamiYomu.Web.Entities;
using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts
{
    public class LibraryDbContext : IDisposable
    {
        private bool _disposed = false;
        private readonly Guid _libraryId;
        private readonly LiteDatabase _database;

        public LibraryDbContext(Guid libraryId)
        {
            _libraryId = libraryId;
            _database = new($"Filename={DatabaseFilePath()};Connection=shared;");
            _database.Mapper.RegisterType<Uri>(
                uri => uri != null ? new BsonValue(uri.ToString()) : BsonValue.Null,
                bson =>
                {
                    var str = bson.AsString;
                    if (string.IsNullOrWhiteSpace(str)) return null;

                    return Uri.TryCreate(str, UriKind.RelativeOrAbsolute, out var uri) ? uri : null;
                }
            );
        }
        public ILiteCollection<ChapterDownloadRecord> ChapterDownloadRecords => _database.GetCollection<ChapterDownloadRecord>("chapter_download_records");
        public ILiteCollection<MangaDownloadRecord> MangaDownloadRecords => _database.GetCollection<MangaDownloadRecord>("manga_download_records");
        public LiteDatabase Raw => _database;

        public string DatabaseFilePath() => $"/db/lib{_libraryId}.db";

        public void DropDatabase()
        {
            _database.Dispose();
            _disposed = true;
            if (File.Exists(DatabaseFilePath()))
            {
                File.Delete(DatabaseFilePath());
            }
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
}
