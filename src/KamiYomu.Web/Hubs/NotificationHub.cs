using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Models;

using Microsoft.AspNetCore.SignalR;

namespace KamiYomu.Web.Hubs;

public class NotificationHub : Hub
{
    public async Task SendNotification(Notification message)
    {
        await Clients.All.SendAsync(Defaults.UI.PushNotification, message);
    }
}
