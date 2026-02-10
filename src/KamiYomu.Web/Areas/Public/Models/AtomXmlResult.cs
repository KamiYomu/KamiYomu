using System.Text;
using System.Xml;
using System.Xml.Serialization;

using Microsoft.AspNetCore.Mvc;

namespace KamiYomu.Web.Areas.Public.Models;

public class AtomXmlResult<T>(T value) : ActionResult
{
    public override async Task ExecuteResultAsync(ActionContext context)
    {
        HttpResponse response = context.HttpContext.Response;
        response.ContentType = "application/atom+xml; charset=utf-8";
        response.StatusCode = StatusCodes.Status200OK;

        XmlSerializer serializer = new(typeof(T));
        XmlSerializerNamespaces namespaces = new();
        namespaces.Add(string.Empty, "http://www.w3.org/2005/Atom");


        await using MemoryStream ms = new();
        using (XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Encoding = Encoding.UTF8,
            Indent = true,
            OmitXmlDeclaration = false
        }))
        {
            serializer.Serialize(writer, value, namespaces);
            writer.Flush();
        }

        ms.Position = 0;

        await ms.CopyToAsync(response.Body);

    }
}
