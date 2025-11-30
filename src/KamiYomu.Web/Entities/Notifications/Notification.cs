using KamiYomu.Web.Entities.Notifications.Definitions;

namespace KamiYomu.Web.Entities.Notifications
{
    public class Notification
    {
        public Notification(string message, NotificationType type)
        {
            Message = message;
            Type = type;
        }

        public string Message { get; init; }
        public NotificationType Type { get; init; }

        public static Notification Success(string message) => new(message, NotificationType.Success);
        public static Notification Info(string message) => new(message, NotificationType.Info);
        public static Notification Warning(string message) => new(message, NotificationType.Warning);
        public static Notification Error(string message) => new(message, NotificationType.Danger);
    }
}
