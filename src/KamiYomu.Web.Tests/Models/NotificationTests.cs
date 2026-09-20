using KamiYomu.Web.Models;
using KamiYomu.Web.Models.Definitions;

namespace KamiYomu.Web.Tests.Models;

public class NotificationTests
{
    [Fact]
    public void Constructor_SetsMessageAndType()
    {
        Notification notification = new("Hello", NotificationType.Info);

        Assert.Equal("Hello", notification.Message);
        Assert.Equal(NotificationType.Info, notification.Type);
    }

    [Theory]
    [InlineData("ok", NotificationType.Success)]
    [InlineData("info", NotificationType.Info)]
    [InlineData("warn", NotificationType.Warning)]
    [InlineData("error", NotificationType.Danger)]
    public void FactoryMethods_CreateNotificationsWithExpectedTypes(string message, NotificationType expectedType)
    {
        Notification notification = expectedType switch
        {
            NotificationType.Success => Notification.Success(message),
            NotificationType.Info => Notification.Info(message),
            NotificationType.Warning => Notification.Warning(message),
            _ => Notification.Error(message)
        };

        Assert.Equal(message, notification.Message);
        Assert.Equal(expectedType, notification.Type);
    }
}
