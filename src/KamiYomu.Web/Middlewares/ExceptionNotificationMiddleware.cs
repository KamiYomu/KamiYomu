using KamiYomu.Web.Entities.Notifications;
using KamiYomu.Web.Infrastructure.Services;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace KamiYomu.Web.Middlewares
{
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

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred.");
                await _notificationService.PushErrorAsync($"An unexpected error occurred. Please try again later. {ex.Message}");
            }
        }


    }
}
