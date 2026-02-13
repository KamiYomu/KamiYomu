using System.Net.Mime;

using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

namespace KamiYomu.Web.Areas.Public.Controllers;

[Area(nameof(Public))]
[Route("[area]/api/v{version:apiVersion}/[controller]")]
[ApiController]
[SwaggerTag(description: "Provides aggregated system statistics for the public API, including counts and " +
                  "metrics related to libraries, chapters, downloads, and crawler activity. " +
                  "Routes are versioned and exposed under the Public area."
)]

public class StatsController(IStatsService statsService) : ControllerBase
{
    [HttpGet]
    [Route("")]
    [Route("~/api/stats")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StatsResponse))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Produces(MediaTypeNames.Application.Json)]
    [SwaggerOperation(
    Summary = "Get application statistics",
    Description = "Returns aggregated system statistics, including counts and metrics related to number of collections, worker activity, version. "
                + "Useful for monitoring overall application health and usage."
    )]
    public IActionResult Get()
    {
        return Ok(statsService.GetStats());
    }
}
