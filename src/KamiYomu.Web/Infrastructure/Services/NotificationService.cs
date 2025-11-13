using KamiYomu.Web.Entities.Notifications;
using KamiYomu.Web.Hubs;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace KamiYomu.Web.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task PushAsync(Notification notification)
        {
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", notification);
        }

        public Task PushErrorAsync(string message)
        {
            return PushAsync(Notification.Error(message));
        }

        public Task PushInfoAsync(string message)
        {
            return PushAsync(Notification.Info(message));
        }

        public Task PushSuccessAsync(string message)
        {
            return PushAsync(Notification.Success(message));
        }

        public Task PushWarningAsync(string message)
        {
           return PushAsync(Notification.Warning(message));
        }
    }

}
