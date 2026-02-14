using System.Xml.Serialization;

namespace KamiYomu.Web.Areas.Public.Models;

public class OpdsCategory
{
    [XmlAttribute("term")]
    public string Term { get; set; }
}
