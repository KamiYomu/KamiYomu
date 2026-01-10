
using Hangfire.Server;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Worker.Interfaces;

namespace KamiYomu.Web.Worker;

public class NotifyKavitaJob(
        ILogger<NotifyKavitaJob> logger,
        DbContext dbContext,
        IKavitaService kavitaService) : INotifyKavitaJob
{
    public Task DispatchAsync(string queue, PerformContext context, CancellationToken cancellationToken)
    {
        try
        {
            UserPreference? preferences = dbContext.UserPreferences.Query().FirstOrDefault();

            if (preferences?.KavitaSettings?.Enabled == true)
            {
                return kavitaService.UpdateAllCollectionsAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
        }
        return Task.CompletedTask;
    }
}
