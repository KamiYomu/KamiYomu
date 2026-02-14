using System.Xml.Serialization;

namespace KamiYomu.Web.Areas.Public.Models;

public class OpdsLink
{
    [XmlAttribute("href")]
    public string Href { get; set; }

    [XmlAttribute("rel")]
    public string Rel { get; set; }

    [XmlAttribute("type")]
    public string Type { get; set; }
}

