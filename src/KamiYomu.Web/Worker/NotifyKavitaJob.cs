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
        UserPreference? preferences = dbContext.UserPreferences.Include(p => p.KavitaSettings).Query().FirstOrDefault();

        return preferences?.KavitaSettings?.Enabled == true ? kavitaService.UpdateAllCollectionsAsync(cancellationToken) : Task.CompletedTask;
    }
}
