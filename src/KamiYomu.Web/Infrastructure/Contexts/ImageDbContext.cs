using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts;

public class ImageDbContext(string connectionString) : IDisposable
{
    private bool _disposed = false;
    public ILiteStorage<Uri> CoverImageFileStorage => Raw.GetStorage<Uri>("_cover_image_file_store", "_cover_images");
    public LiteDatabase Raw => new(connectionString);
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

    ~ImageDbContext()
    {
        Dispose(false);
    }
}
