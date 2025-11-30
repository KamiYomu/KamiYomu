using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using KamiYomu.Web.Infrastructure.Contexts;

namespace KamiYomu.Web.Worker.Attributes
{
    public class MangaCancelOnFailAttribute : JobFilterAttribute, IApplyStateFilter, IDisposable
    {
        public string CancelReason { get; }

        private readonly string _libraryIdParameterName;
        private readonly IServiceScope _scope;
        private readonly ILogger<ChapterCancelOnFailAttribute> _logger;
        private bool disposedValue;

        public MangaCancelOnFailAttribute(string libraryIdParameterName, string cancelReason = "The number of attempts was exceeded")
        {
            CancelReason = cancelReason;
            _libraryIdParameterName = libraryIdParameterName;
            _scope = AppOptions.Defaults.ServiceLocator.Instance.CreateScope();
            _logger = _scope.ServiceProvider.GetRequiredService<ILogger<ChapterCancelOnFailAttribute>>();
        }

        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            var oldState = context.OldStateName;
            var newState = context.NewState?.Name;

            if (oldState == ProcessingState.StateName &&
                newState == FailedState.StateName)
            {
                var args = context.BackgroundJob.Job.Args;
                var method = context.BackgroundJob.Job.Method;
                var parameters = method.GetParameters();

                int index = Array.FindIndex(parameters, p => p.Name == _libraryIdParameterName);

                if (index == -1)
                {
                    _logger.LogError(
                        "MangaCancelOnFail: Parameter '{Parameter}' not found for job {JobId}.",
                        _libraryIdParameterName, context.BackgroundJob.Id);
                    return;
                }

                if(args[index] is Guid libraryId)
                {
                    var jobId = context.BackgroundJob.Id;

                    var dbContext = _scope.ServiceProvider.GetRequiredService<DbContext>();

                    var library = dbContext.Libraries.FindById(libraryId);

                    using var libDbContext = library.GetDbContext();

                    var downloadChapter = libDbContext.MangaDownloadRecords.FindOne(p => p.BackgroundJobId == jobId);

                    if (downloadChapter != null)
                    {
                        downloadChapter.Cancelled(CancelReason);
                        libDbContext.MangaDownloadRecords.Update(downloadChapter);
                    }
                }

                
            }
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            // Not needed
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _scope.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
