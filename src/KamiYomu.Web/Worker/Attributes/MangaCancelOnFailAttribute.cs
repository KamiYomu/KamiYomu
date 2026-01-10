using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

using KamiYomu.Web.Infrastructure.Contexts;

namespace KamiYomu.Web.Worker.Attributes;

public class MangaCancelOnFailAttribute : JobFilterAttribute, IApplyStateFilter, IDisposable
{
    private readonly string _cancelReason;
    private readonly string _libraryIdParameterName;
    private readonly string _titleParameterName;
    private readonly IServiceScope _scope;
    private readonly ILogger<ChapterCancelOnFailAttribute> _logger;
    private bool disposedValue;

    public MangaCancelOnFailAttribute(string libraryIdParameterName,
                                      string titleParameterName,
                                      string cancelReason = "The number of attempts was exceeded")
    {
        _cancelReason = cancelReason;
        _libraryIdParameterName = libraryIdParameterName;
        _titleParameterName = titleParameterName;
        _scope = AppOptions.Defaults.ServiceLocator.Instance.CreateScope();
        _logger = _scope.ServiceProvider.GetRequiredService<ILogger<ChapterCancelOnFailAttribute>>();
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        string oldState = context.OldStateName;
        string? newState = context.NewState?.Name;

        if ((oldState == ProcessingState.StateName &&
            newState == FailedState.StateName) ||
            newState == DeletedState.StateName)
        {
            if (newState == DeletedState.StateName)
            {
                _cancelReason = I18n.DownloadMangaHasBeenCancelled;
            }
            IReadOnlyList<object> args = context.BackgroundJob.Job.Args;
            System.Reflection.MethodInfo method = context.BackgroundJob.Job.Method;
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();

            int libraryIdIndex = Array.FindIndex(parameters, p => p.Name == _libraryIdParameterName);
            int titleIndex = Array.FindIndex(parameters, p => p.Name == _titleParameterName);
            if (libraryIdIndex == -1)
            {
                _logger.LogError(
                    "ChapterCancelOnFail: Parameter '{Parameter}' not found for job {JobId}.",
                    _libraryIdParameterName, context.BackgroundJob.Id);
                return;
            }

            if (args[libraryIdIndex] is Guid libraryId)
            {
                string jobId = context.BackgroundJob.Id;

                DbContext dbContext = _scope.ServiceProvider.GetRequiredService<DbContext>();

                Entities.Library library = dbContext.Libraries.FindById(libraryId);

                if (library != null)
                {
                    using LibraryDbContext libDbContext = library.GetReadWriteDbContext();

                    Entities.MangaDownloadRecord downloadManga = libDbContext.MangaDownloadRecords.FindOne(p => p.BackgroundJobId == jobId);

                    if (downloadManga != null)
                    {
                        downloadManga.Cancelled(_cancelReason);
                        _ = libDbContext.MangaDownloadRecords.Update(downloadManga);
                    }
                }
                _logger.LogWarning("Chapter '{title}' was cancelled. '{newState}' state was applied to the job id '{jobId}'.", args[titleIndex].ToString(), newState, context.BackgroundJob.Id);
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
