using KamiYomu.Web.Infrastructure.Services.Interfaces;
using KamiYomu.Web.ViewComponents;

using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace KamiYomu.Web.Tests.ViewComponents;
public class AppVersionViewComponentTests
{
    private readonly Mock<IGitHubService> _mockGitHubService;
    private readonly AppVersionViewComponent _component;

    public AppVersionViewComponentTests()
    {
        _mockGitHubService = new Mock<IGitHubService>();
        // Instantiate the component with the mocked service
        _component = new AppVersionViewComponent(_mockGitHubService.Object);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsViewWithCorrectModelData()
    {
        // Arrange
        string expectedUpdateVersion = "2.1.0";
        CancellationToken cancellationToken = CancellationToken.None;

        _ = _mockGitHubService
            .Setup(s => s.CheckForUpdatesAsync(It.IsAny<string>(), cancellationToken))
            .ReturnsAsync(true);

        _ = _mockGitHubService
            .Setup(s => s.GetLatestVersionAsync(cancellationToken))
            .ReturnsAsync(expectedUpdateVersion);

        // Act
        Microsoft.AspNetCore.Mvc.IViewComponentResult result = await _component.InvokeAsync(cancellationToken);

        // Assert
        ViewViewComponentResult viewResult = Assert.IsType<ViewViewComponentResult>(result);
        AppVersionViewComponentModel model = Assert.IsType<AppVersionViewComponentModel>(viewResult.ViewData.Model);

        Assert.Equal(expectedUpdateVersion, model.UpdateVersionAvailable);
        Assert.True(model.UpdateAvailable);

        // Verify metadata (these depend on your project's AssemblyInfo)
        Assert.NotNull(model.Version);
        Assert.NotNull(model.Copyright);
    }

    [Fact]
    public async Task InvokeAsync_PassesCurrentVersionToGitHubService()
    {
        // Arrange
        string capturedVersion = null;
        _ = _mockGitHubService
            .Setup(s => s.CheckForUpdatesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((v, ct) => capturedVersion = v)
            .ReturnsAsync(false);

        // Act
        _ = await _component.InvokeAsync();

        // Assert
        // Verify that the service was called with the version string extracted from the assembly
        Assert.False(string.IsNullOrEmpty(capturedVersion));
        _mockGitHubService.Verify(s => s.CheckForUpdatesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

}
