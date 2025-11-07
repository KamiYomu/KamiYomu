using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace KamiYomu.Web.Middlewares
{
    public class ExceptionNotificationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionNotificationMiddleware> _logger;
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public ExceptionNotificationMiddleware(
            RequestDelegate next, 
            ILogger<ExceptionNotificationMiddleware> logger, 
            ITempDataDictionaryFactory tempDataFactory)
        {
            _next = next;
            _logger = logger;
            _tempDataFactory = tempDataFactory;
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
                var tempData = _tempDataFactory.GetTempData(context);
                tempData["ToastError"] = "An unexpected error occurred. Please try again.";
                context.Response.Redirect(context.Request.Path); // Reload current page
            }
        }


    }
}
