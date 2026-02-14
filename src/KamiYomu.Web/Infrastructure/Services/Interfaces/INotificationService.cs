using KamiYomu.Web.Models;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface INotificationService
{
    Task PushAsync(Notification notification, CancellationToken cancellationToken);
    Task PushSuccessAsync(string message, CancellationToken cancellationToken);
    Task PushInfoAsync(string message, CancellationToken cancellationToken);
    Task PushWarningAsync(string message, CancellationToken cancellationToken);
    Task PushErrorAsync(string message, CancellationToken cancellationToken);
    Notification? DequeuePendingNotification();
    void EnqueueForNextPage(Notification notification);
    void EnqueueErrorForNextPage(string message);
    void EnqueueWarningForNextPage(string message);
    void EnqueueInfoForNextPage(string message);
    void EnqueueSuccessForNextPage(string message);
}
