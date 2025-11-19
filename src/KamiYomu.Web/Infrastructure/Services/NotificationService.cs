using KamiYomu.Web.Entities.Notifications;
using KamiYomu.Web.Hubs;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using KamiYomu.Web.AppOptions;
using System.Runtime.CompilerServices;
using KamiYomu.Web.Infrastructure.Contexts;
namespace KamiYomu.Web.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly CacheContext _cacheDbContext;

        public NotificationService(IHubContext<NotificationHub> hubContext,
                                   CacheContext cacheDbContext)
        {
            _hubContext = hubContext;
            _cacheDbContext = cacheDbContext;
        }

        #region Push
        public async Task PushAsync(Notification notification, CancellationToken cancellationToken)
        {
            await _hubContext.Clients.All.SendAsync(Defaults.UI.PushNotification, notification, cancellationToken);
        }

        public Task PushErrorAsync(string message, CancellationToken cancellationToken)
        {
            return PushAsync(Notification.Error(message), cancellationToken);
        }

        public Task PushInfoAsync(string message, CancellationToken cancellationToken)
        {
            return PushAsync(Notification.Info(message), cancellationToken);
        }

        public Task PushSuccessAsync(string message, CancellationToken cancellationToken)
        {
            return PushAsync(Notification.Success(message), cancellationToken);
        }

        public Task PushWarningAsync(string message, CancellationToken cancellationToken)
        {
            return PushAsync(Notification.Warning(message), cancellationToken);
        }
        #endregion

        #region Enqueue
        public void EnqueueForNextPage(Notification notification)
        {
            _cacheDbContext.Current.Add<Notification>(Defaults.UI.EnqueueNotification, notification, expireIn: TimeSpan.FromSeconds(1));
        }

        public Notification? DequeuePendingNotification()
        {
            if(_cacheDbContext.TryGetCached<Notification>(Defaults.UI.EnqueueNotification, out var notification))
            {
                return notification;
            }
            return null;
        }

        public void EnqueueErrorForNextPage(string message)
        {
            EnqueueForNextPage(Notification.Error(message));
        }

        public void EnqueueWarningForNextPage(string message)
        {
            EnqueueForNextPage(Notification.Warning(message));
        }

        public void EnqueueInfoForNextPage(string message)
        {
            EnqueueForNextPage(Notification.Info(message));
        }

        public void EnqueueSuccessForNextPage(string message)
        {
            EnqueueForNextPage(Notification.Success(message));
        }
        #endregion
    }

}
