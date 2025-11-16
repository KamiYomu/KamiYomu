using KamiYomu.Web.Infrastructure.Services.Interfaces;

namespace KamiYomu.Web.Middlewares;
public class ExceptionNotificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionNotificationMiddleware> _logger;
    private readonly INotificationService _notificationService;

    public ExceptionNotificationMiddleware(
        RequestDelegate next,
        ILogger<ExceptionNotificationMiddleware> logger,
        INotificationService notificationService)
    {
        _next = next;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Pass cancellation token from the request
            await _next(context);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Unhandled exception occurred.");

            await _notificationService.PushErrorAsync(
                $"An unexpected error occurred. Please try again later. {ex.Message}",
                context.RequestAborted);
        }
    }
}
