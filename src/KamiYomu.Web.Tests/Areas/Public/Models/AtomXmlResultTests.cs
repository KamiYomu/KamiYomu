using System.Text;

using KamiYomu.Web.Areas.Public.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace KamiYomu.Web.Tests.Areas.Public.Models;

public class AtomXmlResultTests
{
    [Fact]
    public async Task ExecuteResultAsync_WritesAtomXmlWithExpectedContentTypeAndStatusCode()
    {
        OpdsFeed feed = new()
        {
            Id = "urn:opds:manga:list:page:1",
            Title = "KamiYomu Catalog",
            Updated = new DateTime(2024, 1, 1),
            Entries = [new OpdsEntry { Id = "urn:opds:manga:1", Title = "One Piece" }]
        };

        using MemoryStream body = new();
        DefaultHttpContext httpContext = new() { Response = { Body = body } };

        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());

        AtomXmlResult<OpdsFeed> result = new(feed);

        await result.ExecuteResultAsync(actionContext);

        Assert.Equal("application/atom+xml; charset=utf-8", httpContext.Response.ContentType);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);

        body.Position = 0;
        string xml = Encoding.UTF8.GetString(body.ToArray());

        Assert.Contains("urn:opds:manga:list:page:1", xml);
        Assert.Contains("KamiYomu Catalog", xml);
        Assert.Contains("One Piece", xml);
        Assert.StartsWith("<?xml", xml);
    }
}
