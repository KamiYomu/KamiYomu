using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Middlewares;
public class ExceptionNotificationMiddleware(
    RequestDelegate next,
    ILogger<ExceptionNotificationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass cancellation token from the request
            await next(context);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(ex, "Unhandled exception occurred.");

            INotificationService notificationService = context.RequestServices.GetRequiredService<INotificationService>();

            await notificationService.PushErrorAsync(
                $"An unexpected error occurred. Please try again later. {ex.Message}",
                context.RequestAborted);
        }
    }
}
