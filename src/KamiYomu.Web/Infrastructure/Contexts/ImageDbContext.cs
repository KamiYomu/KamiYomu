using LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts;

public class ImageDbContext(string fileName, bool isReadOnly = false) : IDisposable
{
    private bool _disposed = false;
    private readonly Lazy<LiteDatabase> _raw = new(() =>
        new LiteDatabase(new ConnectionString
        {
            Filename = fileName,
            Connection = ConnectionType.Shared,
            ReadOnly = isReadOnly
        }),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    public LiteDatabase Raw => _raw.Value;
    public ILiteStorage<Uri> CoverImageFileStorage => Raw.GetStorage<Uri>("_cover_image_file_store", "_cover_images");


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
