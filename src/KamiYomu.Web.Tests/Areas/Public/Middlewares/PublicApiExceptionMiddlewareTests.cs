using System.Text;
using System.Text.Json;

using KamiYomu.Web.Areas.Public.Middlewares;
using KamiYomu.Web.Areas.Public.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Areas.Public.Middlewares;

public class PublicApiExceptionMiddlewareTests
{
    private readonly Mock<ILogger<PublicApiExceptionMiddleware>> _logger = new();

    [Fact]
    public async Task InvokeAsync_WhenPathIsNotPublic_CallsNextAndDoesNotHandleExceptions()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        PublicApiExceptionMiddleware middleware = new(next, _logger.Object);

        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/settings/api/health";

        await middleware.InvokeAsync(httpContext);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenPathIsNotPublic_DoesNotSwallowException()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("boom");

        PublicApiExceptionMiddleware middleware = new(next, _logger.Object);

        DefaultHttpContext httpContext = new();
        httpContext.Request.Path = "/settings/api/health";

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(httpContext));
    }

    [Theory]
    [InlineData(typeof(ArgumentException), StatusCodes.Status400BadRequest, "bad_request")]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized, "unauthorized")]
    [InlineData(typeof(KeyNotFoundException), StatusCodes.Status404NotFound, "not_found")]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status500InternalServerError, "internal_server_error")]
    public async Task InvokeAsync_WhenPublicPathThrows_WritesMappedErrorResponse(Type exceptionType, int expectedStatusCode, string expectedErrorName)
    {
        Exception exception = (Exception)Activator.CreateInstance(exceptionType, "failure message")!;
        RequestDelegate next = _ => throw exception;

        PublicApiExceptionMiddleware middleware = new(next, _logger.Object);

        using MemoryStream body = new();
        DefaultHttpContext httpContext = new() { Response = { Body = body } };
        httpContext.Request.Path = "/public/api/v1/collection";
        httpContext.TraceIdentifier = "trace-abc";

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(expectedStatusCode, httpContext.Response.StatusCode);
        Assert.Equal("application/json", httpContext.Response.ContentType);

        body.Position = 0;
        string json = Encoding.UTF8.GetString(body.ToArray());
        PublicApiErrorResponse? response = JsonSerializer.Deserialize<PublicApiErrorResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(response);
        Assert.Equal(expectedErrorName, response!.Error);
        Assert.Equal("failure message", response.Message);
        Assert.Equal("trace-abc", response.TraceId);
    }
}
