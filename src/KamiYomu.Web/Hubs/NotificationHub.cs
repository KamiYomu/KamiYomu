using KamiYomu.Web.Entities.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace KamiYomu.Web.Hubs
{
    public class NotificationHub : Hub
    {
        public async Task SendNotification(Notification message)
        {
            await Clients.All.SendAsync("ReceiveNotification", message);
        }
    }

}
