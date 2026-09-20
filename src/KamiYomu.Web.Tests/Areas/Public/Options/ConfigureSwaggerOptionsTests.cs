using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

using KamiYomu.Web.Areas.Public.Options;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace KamiYomu.Web.Tests.Areas.Public.Options;

public class ConfigureSwaggerOptionsTests
{
    [Fact]
    public void Configure_AddsOneSwaggerDocPerApiVersionDescription()
    {
        List<ApiVersionDescription> descriptions =
        [
            new ApiVersionDescription(new ApiVersion(1, 0), "v1", false),
            new ApiVersionDescription(new ApiVersion(2, 0), "v2", false)
        ];

        Mock<IApiVersionDescriptionProvider> provider = new();
        _ = provider.Setup(p => p.ApiVersionDescriptions).Returns(descriptions);

        ConfigureSwaggerOptions configureSwaggerOptions = new(provider.Object);
        SwaggerGenOptions options = new();

        configureSwaggerOptions.Configure(options);

        Assert.Equal(2, options.SwaggerGeneratorOptions.SwaggerDocs.Count);
        Assert.True(options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey("v1"));
        Assert.True(options.SwaggerGeneratorOptions.SwaggerDocs.ContainsKey("v2"));
        Assert.Equal("KamiYomu API", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Title);
        Assert.Equal("1.0", options.SwaggerGeneratorOptions.SwaggerDocs["v1"].Version);
        Assert.Equal("2.0", options.SwaggerGeneratorOptions.SwaggerDocs["v2"].Version);
    }

    [Fact]
    public void Configure_WithNoApiVersions_AddsNoSwaggerDocs()
    {
        Mock<IApiVersionDescriptionProvider> provider = new();
        _ = provider.Setup(p => p.ApiVersionDescriptions).Returns([]);

        ConfigureSwaggerOptions configureSwaggerOptions = new(provider.Object);
        SwaggerGenOptions options = new();

        configureSwaggerOptions.Configure(options);

        Assert.Empty(options.SwaggerGeneratorOptions.SwaggerDocs);
    }
}
