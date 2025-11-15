using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Middlewares;

public class ExceptionNotificationMiddleware(
    RequestDelegate next,
    ILogger<ExceptionNotificationMiddleware> logger,
    INotificationService notificationService)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred.");
            await notificationService.PushErrorAsync($"An unexpected error occurred. Please try again later. {ex.Message}");
        }
    }


}
