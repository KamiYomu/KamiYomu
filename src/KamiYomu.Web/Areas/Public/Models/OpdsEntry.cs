using System.Xml.Serialization;

using KamiYomu.Web.Entities;
using KamiYomu.Web.Extensions;

namespace KamiYomu.Web.Areas.Public.Models;
public class OpdsEntry
{
    public static OpdsEntry Create(Library library)
    {
        return new OpdsEntry
        {
            Id = $"urn:manga:{library.Id}",
            Title = library.Manga.Title,
            Updated = library.CreatedDate.ToLocalTime().DateTime,
            Summary = library.Manga.Description,
            Categories = library.Manga.Tags?.Select(tag => new OpdsCategory { Term = tag }).ToList() ?? [],
            Links =
            [
                new OpdsLink
                {
                    Href = $"/public/api/v1/opds/{library.Id}",
                    Rel = "alternate",
                    Type = "application/atom+xml"
                },
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(), // URL to the cover image
                    Rel = "http://opds-spec.org/image",
                    Type = "image/jpeg"
                },
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                    Rel = "http://opds-spec.org/image/thumbnail",
                    Type = "image/jpeg"
                }
            ],
            Publisher = library.Manga.Authors.FirstOrDefault(),
            Language = library.Manga.OriginalLanguage
        };
    }

    internal static OpdsEntry Create(Library library, ChapterDownloadRecord chapterDownloadRecord)
    {
        return new OpdsEntry
        {
            Id = $"urn:manga:{library.Id}:chapters:{chapterDownloadRecord.Id}",
            Title = library.GetComicInfoTitleTemplateResolved(chapterDownloadRecord.Chapter),
            Updated = chapterDownloadRecord.StatusUpdateAt.Value.ToLocalTime().DateTime,
            Summary = library.Manga.Description,
            Categories = library.Manga.Tags?.Select(tag => new OpdsCategory { Term = tag }).ToList() ?? [],
            Links =
            [
                new OpdsLink
                {
                    Href = $"/public/api/v1/opds/{library.Id}/chapters/{chapterDownloadRecord.Id}/download?format=comic",
                    Rel = "http://opds-spec.org/acquisition",
                    Type = "application/vnd.comicbook+zip"
                },
                new OpdsLink
                {
                    Href = $"/public/api/v1/opds/{library.Id}/chapters/{chapterDownloadRecord.Id}/download/epub",
                    Rel = "http://opds-spec.org/acquisition",
                    Type = "application/epub+zip"
                },
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(), // URL to the cover image
                    Rel = "http://opds-spec.org/image",
                    Type = "image/jpeg"
                },
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                    Rel = "http://opds-spec.org/image/thumbnail",
                    Type = "image/jpeg"
                }
            ]
        };
    }
    internal static List<OpdsEntry> CreateChapterEntries(Library library, IEnumerable<ChapterDownloadRecord> chapterDownloadRecords)
    {
        return [.. chapterDownloadRecords.Select(record => new OpdsEntry
        {
            Id = $"urn:manga:{library.Id}:chapters:{record.Id}",
            Title = library.GetComicInfoTitleTemplateResolved(record.Chapter),
            Updated = record.StatusUpdateAt?.ToLocalTime().DateTime ?? DateTime.UtcNow,
            Summary = library.Manga.Description,
            Categories = library.Manga.Tags?.Select(tag => new OpdsCategory { Term = tag }).ToList() ?? [],
            Links =
        [
            new OpdsLink
            {
                Href = $"/public/api/v1/opds/{library.Id}/chapters/{record.Id}/download?format=comic",
                Rel = "http://opds-spec.org/acquisition",
                Type = "application/vnd.comicbook+zip"
            },
            new OpdsLink
            {
                Href = $"/public/api/v1/opds/{library.Id}/chapters/{record.Id}/download/epub",
                Rel = "http://opds-spec.org/acquisition",
                Type = "application/epub+zip"
            },
             new OpdsLink
            {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                    Rel = "http://opds-spec.org/image",
                    Type = "image/jpeg"
            },
            new OpdsLink
            {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                    Rel = "http://opds-spec.org/image/thumbnail",
                    Type = "image/jpeg"
            }
        ]
        })];
    }


    [XmlElement("id")]
    public string Id { get; set; }

    [XmlElement("title")]
    public string Title { get; set; }

    [XmlElement("updated")]
    public DateTime Updated { get; set; }

    [XmlElement("summary")]
    public string Summary { get; set; }

    [XmlElement("link")]
    public List<OpdsLink> Links { get; set; } = [];

    [XmlElement("category")]
    public List<OpdsCategory> Categories { get; set; } = [];

    [XmlElement("publisher", Namespace = "http://purl.org/dc/terms/")]
    public string Publisher { get; set; }

    [XmlElement("language", Namespace = "http://purl.org/dc/terms/")]
    public string Language { get; set; }

    [XmlElement("issued", Namespace = "http://purl.org/dc/terms/")]
    public DateTime? Issued { get; set; }
}
