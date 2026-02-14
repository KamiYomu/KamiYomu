using System.Text.Json;

using KamiYomu.Web.Areas.Public.Models;

namespace KamiYomu.Web.Areas.Public.Middlewares;


public sealed class PublicApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<PublicApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/public"))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            string traceId = context.TraceIdentifier;

            logger.LogError(ex,
                "Public API exception. TraceId: {TraceId}, Path: {Path}",
                traceId,
                context.Request.Path);

            await WriteErrorResponse(context, ex, traceId);
        }
    }

    private static async Task WriteErrorResponse(
        HttpContext context,
        Exception exception,
        string traceId)
    {
        int statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        PublicApiErrorResponse response = new()
        {
            Error = GetErrorName(statusCode),
            Message = exception.Message,
            TraceId = traceId,
            Timestamp = DateTime.UtcNow
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        string json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static string GetErrorName(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "bad_request",
            StatusCodes.Status401Unauthorized => "unauthorized",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status500InternalServerError => "internal_server_error",
            _ => "error"
        };
    }
}
