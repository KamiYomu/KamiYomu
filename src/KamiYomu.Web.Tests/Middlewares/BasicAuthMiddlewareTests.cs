using System.Text;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Middlewares;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Tests.Middlewares;

public class BasicAuthMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_CallsNextWhenAuthenticationIsDisabled()
    {
        bool nextCalled = false;
        BasicAuthMiddleware middleware = CreateMiddleware(
            options: new BasicAuthOptions
            {
                Enabled = false,
                AdminUsername = "admin",
                AdminPassword = "password"
            },
            next: context =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ChallengesWhenAuthorizationHeaderIsMissing()
    {
        bool nextCalled = false;
        BasicAuthMiddleware middleware = CreateMiddleware(
            options: EnabledOptions(),
            next: context =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        DefaultHttpContext context = new();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("Basic realm=\"KamiYomu\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ChallengesWhenCredentialsAreInvalid()
    {
        BasicAuthMiddleware middleware = CreateMiddleware(EnabledOptions(), context => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = $"Basic {Encode("admin:wrong-password")}";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("Basic realm=\"KamiYomu\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ChallengesWhenAuthorizationHeaderIsMalformed()
    {
        BasicAuthMiddleware middleware = CreateMiddleware(EnabledOptions(), context => Task.CompletedTask);
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = "Basic invalid-base64";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("Basic realm=\"KamiYomu\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task InvokeAsync_SetsPrincipalAndCallsNextForValidCredentials()
    {
        bool nextCalled = false;
        BasicAuthMiddleware middleware = CreateMiddleware(
            EnabledOptions(),
            context =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        DefaultHttpContext context = new();
        context.Request.Headers.Authorization = $"Basic {Encode("admin:password")}";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal("admin", context.User.Identity?.Name);
        Assert.Equal("Basic", context.User.Identity?.AuthenticationType);
    }

    private static BasicAuthMiddleware CreateMiddleware(BasicAuthOptions options, RequestDelegate next)
    {
        return new BasicAuthMiddleware(next, Options.Create(options));
    }

    private static BasicAuthOptions EnabledOptions()
    {
        return new BasicAuthOptions
        {
            Enabled = true,
            AdminUsername = "admin",
            AdminPassword = "password"
        };
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }
}
