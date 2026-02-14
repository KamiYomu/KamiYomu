using System.Xml.Serialization;

namespace KamiYomu.Web.Areas.Public.Models;
[XmlRoot("feed", Namespace = "http://www.w3.org/2005/Atom")]
public class OpdsFeed
{
    [XmlElement("id")]
    public string Id { get; set; }

    [XmlElement("title")]
    public string Title { get; set; }
    [XmlElement("icon")]
    public string Icon { get; set; }

    [XmlElement("updated")]
    public DateTime Updated { get; set; }

    [XmlElement("entry")]
    public List<OpdsEntry> Entries { get; set; } = [];

    [XmlElement("link")]
    public List<OpdsLink> Links { get; set; } = [];
}


