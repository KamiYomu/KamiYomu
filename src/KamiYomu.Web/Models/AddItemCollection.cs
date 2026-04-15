using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.Annotations;

namespace KamiYomu.Web.Models;
/// <summary>
/// Add item collection request model. 
/// This model is used to add a new item collection for a crawler agent. 
/// It contains the necessary information to create a new item collection, 
/// such as the crawler agent ID, manga ID, file path template, comic info title template, comic info series template, 
/// and whether to make this configuration default.
/// </summary>
public record AddItemCollection
{
    /// <summary>
    /// Crawler Agent Id. This is the unique identifier of the crawler agent for which the item collection is being added.
    /// </summary>
    [SwaggerRequestBody("Crawler Agent Id. This is the unique identifier of the crawler agent for which the item collection is being added.", Required = true)]
    public Guid CrawlerAgentId { get; set; }
    /// <summary>
    /// Gets or sets the unique identifier for the manga.
    /// </summary>
    [SwaggerRequestBody("Gets or sets the unique identifier for the manga.", Required = true)]
    public string MangaId { get; set; }
    /// <summary>
    /// Gets or sets the file path template for the manga. 
    /// This template is used to generate the file path for the manga when it is downloaded.
    /// </summary>
    public string? FilePathTemplate { get; set; } = null;
    /// <summary>
    /// Gets or sets the template string used to generate the title for ComicInfo metadata.
    /// </summary>
    public string? ComicInfoTitleTemplate { get; set; } = null;
    /// <summary>
    /// Gets or sets the template string used to generate the series name for ComicInfo metadata.
    /// </summary>
    public string? ComicInfoSeriesTemplate { get; set; } = null;
    /// <summary>
    /// Gets or sets a value indicating whether this configuration should be set as the default for future operations.
    /// </summary>
    public bool MakeThisConfigurationDefault { get; set; } = false;
}
