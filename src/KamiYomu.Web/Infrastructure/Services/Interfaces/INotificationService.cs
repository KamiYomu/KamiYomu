using KamiYomu.Web.Entities.Notifications;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces
{
    public interface INotificationService
    {
        Task PushAsync(Notification notification);
        Task PushSuccessAsync(string message);
        Task PushInfoAsync(string message);
        Task PushWarningAsync(string message);
        Task PushErrorAsync(string message);
    }
}
