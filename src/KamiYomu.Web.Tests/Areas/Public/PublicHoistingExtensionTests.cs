using Asp.Versioning.ApiExplorer;

using KamiYomu.Web.Areas.Public;
using KamiYomu.Web.Areas.Public.Options;

using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace KamiYomu.Web.Tests.Areas.Public;

// NOTE: UsePublicArea is intentionally not covered here. It wires together Swagger UI,
// request timeouts and a custom middleware directly against an IApplicationBuilder /
// WebApplication pipeline; exercising it would require standing up a near-complete host
// (TestServer/WebApplicationFactory), which is disproportionate for what is essentially
// framework plumbing with no branching logic of its own. AddPublicArea below verifies the
// DI wiring it depends on.
public class PublicHoistingExtensionTests
{
    [Fact]
    public void AddPublicArea_RegistersControllerAndVersioningServices()
    {
        ServiceCollection services = new();
        _ = services.AddLogging();

        IServiceCollection result = services.AddPublicArea();

        Assert.Same(services, result);

        ServiceProvider provider = services.BuildServiceProvider();

        IApiVersionDescriptionProvider apiVersionDescriptionProvider = provider.GetRequiredService<IApiVersionDescriptionProvider>();
        Assert.NotNull(apiVersionDescriptionProvider);

        IActionDescriptorCollectionProvider actionDescriptorCollectionProvider = provider.GetRequiredService<IActionDescriptorCollectionProvider>();
        Assert.NotNull(actionDescriptorCollectionProvider);

        IEnumerable<IConfigureOptions<SwaggerGenOptions>> swaggerConfigurations = provider.GetServices<IConfigureOptions<SwaggerGenOptions>>();
        Assert.Contains(swaggerConfigurations, c => c is ConfigureSwaggerOptions);

        IOptions<RequestTimeoutOptions> requestTimeoutOptions = provider.GetRequiredService<IOptions<RequestTimeoutOptions>>();
        Assert.Equal(TimeSpan.FromSeconds(30), requestTimeoutOptions.Value.DefaultPolicy.Timeout);
        Assert.True(requestTimeoutOptions.Value.Policies.ContainsKey("CrawlerAgentSearchPolicy"));
    }
}
