using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts
{
    public class ImageDbContext : IDisposable
    {
        private bool _disposed = false;
        private readonly LiteDatabase _database;
        public ImageDbContext(string connectionString)
        {
            _database = new(connectionString);
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

        public ILiteStorage<Uri> CoverImageFileStorage => _database.GetStorage<Uri>("_cover_image_file_store", "_cover_images");
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

        ~ImageDbContext()
        {
            Dispose(false);
        }
    }
}
