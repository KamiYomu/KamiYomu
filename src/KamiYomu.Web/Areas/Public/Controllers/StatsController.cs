using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Public.Controllers;

[Area(nameof(Public))]
[Route("[area]/api/v{version:apiVersion}/[controller]")]
[ApiController]
public class StatsController(IStatsService statsService) : ControllerBase
{
    [HttpGet]
    [Route("")]
    [Route("~/api/stats")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StatsResponse))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Produces("application/json")]
    public IActionResult Get()
    {
        return Ok(statsService.GetStats());
    }
}
