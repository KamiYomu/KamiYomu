using KamiYomu.Web.Hubs;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Models;
using KamiYomu.Web.Models.Definitions;

using Microsoft.AspNetCore.SignalR;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task PushAsync_SendsNotificationToAllClients()
    {
        ServiceTestHelpers.InitializeCache(nameof(NotificationServiceTests));

        Notification captured = null!;
        string? methodName = null;

        Mock<IClientProxy> clientProxy = new();
        _ = clientProxy
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                methodName = method;
                captured = Assert.IsType<Notification>(args[0]);
            })
            .Returns(Task.CompletedTask);

        Mock<IHubClients> clients = new();
        _ = clients.SetupGet(x => x.All).Returns(clientProxy.Object);

        Mock<IHubContext<NotificationHub>> hubContext = new();
        _ = hubContext.SetupGet(x => x.Clients).Returns(clients.Object);

        NotificationService service = new(hubContext.Object, new CacheContext());
        Notification notification = Notification.Success("done");

        await service.PushAsync(notification, CancellationToken.None);

        Assert.Equal(KamiYomu.Web.AppOptions.Defaults.UI.PushNotification, methodName);
        Assert.Equal("done", captured.Message);
        Assert.Equal(NotificationType.Success, captured.Type);
    }

    [Theory]
    [InlineData("error", NotificationType.Danger, "PushErrorAsync")]
    [InlineData("info", NotificationType.Info, "PushInfoAsync")]
    [InlineData("success", NotificationType.Success, "PushSuccessAsync")]
    [InlineData("warning", NotificationType.Warning, "PushWarningAsync")]
    public async Task PushConvenienceMethods_SendExpectedNotificationType(string message, NotificationType expectedType, string methodName)
    {
        ServiceTestHelpers.InitializeCache($"{nameof(NotificationServiceTests)}-{methodName}");

        Notification captured = null!;

        Mock<IClientProxy> clientProxy = new();
        _ = clientProxy
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) =>
            {
                captured = Assert.IsType<Notification>(args[0]);
            })
            .Returns(Task.CompletedTask);

        Mock<IHubClients> clients = new();
        _ = clients.SetupGet(x => x.All).Returns(clientProxy.Object);

        Mock<IHubContext<NotificationHub>> hubContext = new();
        _ = hubContext.SetupGet(x => x.Clients).Returns(clients.Object);

        NotificationService service = new(hubContext.Object, new CacheContext());

        switch (methodName)
        {
            case "PushErrorAsync":
                await service.PushErrorAsync(message, CancellationToken.None);
                break;
            case "PushInfoAsync":
                await service.PushInfoAsync(message, CancellationToken.None);
                break;
            case "PushSuccessAsync":
                await service.PushSuccessAsync(message, CancellationToken.None);
                break;
            default:
                await service.PushWarningAsync(message, CancellationToken.None);
                break;
        }

        Assert.Equal(message, captured.Message);
        Assert.Equal(expectedType, captured.Type);
    }

    [Fact]
    public void EnqueueForNextPage_AndDequeuePendingNotification_RoundTripsNotification()
    {
        ServiceTestHelpers.InitializeCache($"{nameof(NotificationServiceTests)}-queue");

        NotificationService service = CreateServiceWithoutHub();
        Notification notification = Notification.Info("queued");

        service.EnqueueForNextPage(notification);
        Notification? result = service.DequeuePendingNotification();

        Assert.NotNull(result);
        Assert.Equal("queued", result.Message);
        Assert.Equal(NotificationType.Info, result.Type);
    }

    [Theory]
    [InlineData("Error", NotificationType.Danger)]
    [InlineData("Info", NotificationType.Info)]
    [InlineData("Success", NotificationType.Success)]
    [InlineData("Warning", NotificationType.Warning)]
    public void EnqueueConvenienceMethods_StoreExpectedNotification(string methodName, NotificationType expectedType)
    {
        ServiceTestHelpers.InitializeCache($"{nameof(NotificationServiceTests)}-{methodName}-queue");

        NotificationService service = CreateServiceWithoutHub();

        switch (methodName)
        {
            case "Error":
                service.EnqueueErrorForNextPage(methodName);
                break;
            case "Info":
                service.EnqueueInfoForNextPage(methodName);
                break;
            case "Success":
                service.EnqueueSuccessForNextPage(methodName);
                break;
            default:
                service.EnqueueWarningForNextPage(methodName);
                break;
        }

        Notification? notification = service.DequeuePendingNotification();

        Assert.NotNull(notification);
        Assert.Equal(methodName, notification.Message);
        Assert.Equal(expectedType, notification.Type);
    }

    private static NotificationService CreateServiceWithoutHub()
    {
        Mock<IHubClients> clients = new();
        Mock<IHubContext<NotificationHub>> hubContext = new();
        _ = hubContext.SetupGet(x => x.Clients).Returns(clients.Object);
        return new NotificationService(hubContext.Object, new CacheContext());
    }
}
