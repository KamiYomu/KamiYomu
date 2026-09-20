using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.Middlewares;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KamiYomu.Web.Tests.Middlewares;

public class ExceptionNotificationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CallsNextWhenNoExceptionIsThrown()
    {
        bool nextCalled = false;
        ExceptionNotificationMiddleware middleware = new(
            context =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new Mock<ILogger<ExceptionNotificationMiddleware>>().Object);
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_PushesErrorNotificationWhenUnhandledExceptionOccurs()
    {
        Mock<INotificationService> notificationService = new();
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton(notificationService.Object)
            .BuildServiceProvider();
        ExceptionNotificationMiddleware middleware = new(
            context => throw new InvalidOperationException("boom"),
            new Mock<ILogger<ExceptionNotificationMiddleware>>().Object);
        DefaultHttpContext context = new()
        {
            RequestServices = serviceProvider
        };

        await middleware.InvokeAsync(context);

        notificationService.Verify(
            x => x.PushErrorAsync(
                It.Is<string>(message => message.Contains("An unexpected error occurred. Please try again later. boom", StringComparison.Ordinal)),
                context.RequestAborted),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_RethrowsExceptionWhenRequestHasBeenAborted()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();
        ExceptionNotificationMiddleware middleware = new(
            context => throw new InvalidOperationException("boom"),
            new Mock<ILogger<ExceptionNotificationMiddleware>>().Object);
        DefaultHttpContext context = new()
        {
            RequestAborted = cancellationTokenSource.Token
        };

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }
}
