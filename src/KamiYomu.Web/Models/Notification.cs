using KamiYomu.Web.Models.Definitions;

namespace KamiYomu.Web.Models;

public class Notification(string message, NotificationType type)
{
    public string Message { get; init; } = message;
    public NotificationType Type { get; init; } = type;

    public static Notification Success(string message)
    {
        return new(message, NotificationType.Success);
    }

    public static Notification Info(string message)
    {
        return new(message, NotificationType.Info);
    }

    public static Notification Warning(string message)
    {
        return new(message, NotificationType.Warning);
    }

    public static Notification Error(string message)
    {
        return new(message, NotificationType.Danger);
    }
}
