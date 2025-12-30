using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Services;

public class LockManager : ILockManager
{
    private readonly WorkerOptions _workerOptions;
    private readonly string _folder;

    public LockManager(IOptions<WorkerOptions> workerOptions, IHostEnvironment env)
    {
        _workerOptions = workerOptions.Value;

        _folder = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, "locks");
        _ = Directory.CreateDirectory(_folder);
    }

    public IDisposable? TryAcquireAsync(string crawlerId)
    {
        for (int slot = 1; slot <= _workerOptions.MaxConcurrentCrawlerInstances; slot++)
        {
            string path = Path.Combine(_folder, $"{crawlerId}-{slot}.lock");

            if (TryAcquireSlot(path, out IDisposable? handle))
            {
                return handle;
            }
        }

        return null;
    }

    private bool TryAcquireSlot(string path, out IDisposable? handle)
    {
        handle = null;

        try
        {
            if (File.Exists(path))
            {
                DateTime lastWrite = File.GetLastWriteTimeUtc(path);

                if ((DateTime.UtcNow - lastWrite).TotalMinutes > Defaults.Worker.StaleLockTimeout)
                {
                    TryDelete(path);
                }
            }

            FileStream fs = new(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.None);

            using (StreamWriter writer = new(fs, leaveOpen: true))
            {
                fs.SetLength(0);
                writer.Write(DateTime.UtcNow.ToString("o"));
                writer.Flush();
            }

            handle = new FileLockHandle(fs, path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { }
    }

    private class FileLockHandle : IDisposable
    {
        private readonly FileStream _stream;
        private readonly string _path;

        public FileLockHandle(FileStream stream, string path)
        {
            _stream = stream;
            _path = path;
        }

        public void Dispose()
        {
            try { _stream.Dispose(); }
            catch { }

            try { File.Delete(_path); }
            catch { }
        }
    }
}
