using System.Net.Mime;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Areas.Public.Models;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Repositories.Interfaces;

using LiteDB;

using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using static KamiYomu.Web.AppOptions.Defaults;

namespace KamiYomu.Web.Areas.Public.Controllers;

[Area(nameof(Public))]
[Route("[area]/api/v{version:apiVersion}/[controller]")]
[ApiController]
[SwaggerTag(description: "Provides access to crawler agents exposed through the public API, including " +
                  "retrieving agent metadata, listing downloadable content, and performing " +
                  "other operations. Routes are versioned and organized under the " +
                  "Public area."
)]
public class CrawlerAgentController(ICrawlerAgentRepository crawlerAgentRepository) : ControllerBase
{
    [HttpGet]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(IEnumerable<CrawlerAgentItem>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "List crawler agents",
        Description = "Returns a paginated list of crawler agents filtered by search text."
    )]
    public IActionResult List(
        [FromQuery(Name = "search")] string? search,
        [FromQuery(Name = "offset")] int offSet = 0,
        [FromQuery(Name = "limit")] int limit = 20,
        [FromKeyedServices(ServiceLocator.ReadOnlyDbContext)] DbContext dbContext = default!)
    {
        ILiteQueryable<CrawlerAgent> query = dbContext.CrawlerAgents.Query();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.DisplayName.Contains(search) || p.AssemblyName.Contains(search));
        }

        return Ok(query.Offset(offSet)
                    .Limit(limit)
                    .ToList()
                    .Select(p =>
                    new CrawlerAgentItem
                    {
                        AssemblyName = p.AssemblyName,
                        AgentMetadata = p.AgentMetadata,
                        AssemblyProperties = p.AssemblyProperties,
                        DisplayName = p.DisplayName,
                        Id = p.Id,
                    }));
    }


    [HttpGet]
    [Route("{crawlerAgentId:guid}/list-downloadable-content")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(PagedResult<Manga>), StatusCodes.Status200OK)]
    [SwaggerOperation(
    Summary = "List downloadable content for a crawler agent",
    Description = @"Retrieves downloadable content associated with the specified crawler agent.  
                    Supports search filtering, offset/limit pagination, and continuation tokens
                    for incremental retrieval."
)]
    public async Task<IActionResult> ListDownloadableContentAsync(
        [FromRoute(Name = "crawlerAgentId")] Guid crawlerAgentId,
        [FromQuery(Name = "search")] string search,
        [FromQuery(Name = "offset")] int? offSet = null,
        [FromQuery(Name = "limit")] int? limit = null,
        [FromQuery(Name = "continuationToken")] string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        if (crawlerAgentId == Guid.Empty)
        {
            return NotFound();
        }

        PaginationOptions paginationOptions = new();

        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            paginationOptions = new PaginationOptions(continuationToken, limit);
        }
        else if (offSet > -1 && limit > 0)
        {
            paginationOptions = new PaginationOptions(offSet, limit, limit);
        }

        PagedResult<Manga> pagedResult = await crawlerAgentRepository.SearchAsync(crawlerAgentId, search, paginationOptions, cancellationToken);

        return Ok(pagedResult);
    }

}
