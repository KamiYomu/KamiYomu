using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Hubs;
using KamiYomu.Web.Models;
using KamiYomu.Web.Models.Definitions;

using Microsoft.AspNetCore.SignalR;

namespace KamiYomu.Web.Tests.Hubs;

public class NotificationHubTests
{
    [Fact]
    public async Task SendNotification_SendsNotificationToAllClients()
    {
        Notification notification = new("Hello", NotificationType.Success);
        Mock<IHubCallerClients> clients = new();
        Mock<IClientProxy> proxy = new();
        NotificationHub hub = new()
        {
            Clients = clients.Object
        };

        _ = clients.Setup(x => x.All).Returns(proxy.Object);

        await hub.SendNotification(notification);

        proxy.Verify(
            x => x.SendCoreAsync(
                Defaults.UI.PushNotification,
                It.Is<object[]>(args => args.Length == 1 && ReferenceEquals(args[0], notification)),
                default),
            Times.Once);
    }
}
