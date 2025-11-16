using KamiYomu.Web.Entities.Notifications;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces
{
    public interface INotificationService
    {
        Task PushAsync(Notification notification, CancellationToken cancellationToken);
        Task PushSuccessAsync(string message, CancellationToken cancellationToken);
        Task PushInfoAsync(string message, CancellationToken cancellationToken);
        Task PushWarningAsync(string message, CancellationToken cancellationToken);
        Task PushErrorAsync(string message, CancellationToken cancellationToken);
        Notification? Dequeue();
        void Enqueue(Notification notification);
        void EnqueueError(string message);
        void EnqueueWarning(string message);
        void EnqueueInfo(string message);
        void EnqueueSuccess(string message);
    }
}
