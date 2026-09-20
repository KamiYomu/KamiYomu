using KamiYomu.Web.Areas.Public.Controllers;
using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Tests.Areas.Public.Controllers;

public class StatsControllerTests
{
    [Fact]
    public void Get_ReturnsOkWithStatsFromService()
    {
        StatsResponse stats = new("1.0.0", "2.0.0", 5, 3, 1);
        Mock<IStatsService> statsService = new();
        _ = statsService.Setup(s => s.GetStats()).Returns(stats);

        StatsController controller = new(statsService.Object);

        IActionResult result = controller.Get();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(stats, ok.Value);
        statsService.Verify(s => s.GetStats(), Times.Once);
    }
}
