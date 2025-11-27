using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Infrastructure.Services
{
    public class LockManager : ILockManager
    {
        private readonly WorkerOptions _workerOptions;
        private readonly string _folder;

        public LockManager(IOptions<WorkerOptions> workerOptions, IHostEnvironment env)
        {
            _workerOptions = workerOptions.Value;

            _folder = Path.Combine(env.ContentRootPath, "crawler-locks");
            Directory.CreateDirectory(_folder);
        }

        public Task<IDisposable?> TryAcquireAsync(string crawlerId)
        {

            for (int slot = 1; slot <= _workerOptions.MaxConcurrentCrawlerInstances; slot++)
            {
                string path = Path.Combine(_folder, $"{crawlerId}-{slot}.lock");

                try
                {
                    var fs = new FileStream(
                        path,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        1,
                        FileOptions.DeleteOnClose);

                    return Task.FromResult<IDisposable?>(new FileLockHandle(fs));
                }
                catch (IOException)
                {
                }
            }

            return Task.FromResult<IDisposable?>(null);
        }

        private class FileLockHandle : IDisposable
        {
            private readonly FileStream _stream;

            public FileLockHandle(FileStream stream)
            {
                _stream = stream;
            }

            public void Dispose() => _stream.Dispose();
        }
    }
}
