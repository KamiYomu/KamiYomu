using KamiYomu.Web.AppOptions;

using Microsoft.Extensions.Options;

using System.Security.Claims;
using System.Text;

namespace KamiYomu.Web.Middlewares;
public class BasicAuthMiddleware(RequestDelegate next, IOptions<BasicAuthOptions> options)
{
    private readonly BasicAuthOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await next(context);
            return;
        }

        string authHeader = context.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            Challenge(context);
            return;
        }

        try
        {
            string encoded = authHeader["Basic ".Length..].Trim();
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            string[] parts = decoded.Split(':', 2);

            if (parts.Length != 2)
            {
                Challenge(context);
                return;
            }

            string username = parts[0];
            string password = parts[1];

            if (username != _options.AdminUsername || password != _options.AdminPassword)
            {
                Challenge(context);
                return;
            }

            Claim[] claims = new[]
            {
                new Claim(ClaimTypes.Name, username)
            };
            ClaimsIdentity identity = new(claims, "Basic");
            context.User = new ClaimsPrincipal(identity);

            await next(context);
        }
        catch
        {
            Challenge(context);
        }
    }

    private static void Challenge(HttpContext context)
    {
        context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"KamiYomu\"";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }
}
