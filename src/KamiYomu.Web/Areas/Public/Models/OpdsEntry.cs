using System.Net.Mime;
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
            Id = $"urn:opds:manga:{library.Id}",
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
                    Type = MediaTypeNames.Image.Jpeg
                },
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                    Rel = "http://opds-spec.org/image/thumbnail",
                    Type =  MediaTypeNames.Image.Jpeg
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
            Id = $"urn:opds:manga:{library.Id}:chapters:{chapterDownloadRecord.Id}",
            Title = library.GetComicInfoTitleTemplateResolved(chapterDownloadRecord.Chapter),
            Updated = chapterDownloadRecord.StatusUpdateAt.Value.ToLocalTime().DateTime,
            Summary = library.Manga.Description,
            Categories = library.Manga.Tags?.Select(tag => new OpdsCategory { Term = tag }).ToList() ?? [],
            Links =
            [
                .. GetOpdsLinks(library.Id, chapterDownloadRecord.Id),
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(), // URL to the cover image
                    Rel = "http://opds-spec.org/image",
                    Type = MediaTypeNames.Image.Svg
                },
                new OpdsLink
                {
                    Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                    Rel = "http://opds-spec.org/image/thumbnail",
                    Type = MediaTypeNames.Image.Jpeg
                }
            ]
        };
    }
    internal static List<OpdsEntry> CreateChapterEntries(Library library, IEnumerable<ChapterDownloadRecord> chapterDownloadRecords)
    {
        return [.. chapterDownloadRecords.Select(record => new OpdsEntry
        {
            Id = $"urn:opds:manga:{library.Id}:chapters:{record.Id}",
            Title = library.GetComicInfoTitleTemplateResolved(record.Chapter),
            Updated = record.StatusUpdateAt?.ToLocalTime().DateTime ?? DateTime.UtcNow,
            Summary = library.Manga.Description,
            Categories = library.Manga.Tags?.Select(tag => new OpdsCategory { Term = tag }).ToList() ?? [],
            Links =
            [
                .. GetOpdsLinks(library.Id, record.Id),
                new OpdsLink
                {
                        Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                        Rel = "http://opds-spec.org/image",
                        Type = MediaTypeNames.Image.Jpeg
                },
                new OpdsLink
                {
                        Href = library.Manga.CoverUrl.ToInternalImageUrl().ToString(),
                        Rel = "http://opds-spec.org/image/thumbnail",
                        Type = MediaTypeNames.Image.Jpeg
                }
            ]
        })];
    }


    private static IEnumerable<OpdsLink> GetOpdsLinks(Guid libraryId, Guid chapterDownloadRecordId)
    {
        yield return new OpdsLink
        {
            Href = $"/public/api/v1/opds/{libraryId}/chapters/{chapterDownloadRecordId}/download/cbz",
            Rel = "http://opds-spec.org/acquisition",
            Type = "application/vnd.comicbook+zip"
        };
        yield return new OpdsLink
        {
            Href = $"/public/api/v1/opds/{libraryId}/chapters/{chapterDownloadRecordId}/download/zip",
            Rel = "http://opds-spec.org/acquisition",
            Type = MediaTypeNames.Application.Zip
        };
        yield return new OpdsLink
        {
            Href = $"/public/api/v1/opds/{libraryId}/chapters/{chapterDownloadRecordId}/download/epub",
            Rel = "http://opds-spec.org/acquisition",
            Type = "application/epub+zip"
        };
        yield return new OpdsLink
        {
            Href = $"/public/api/v1/opds/{libraryId}/chapters/{chapterDownloadRecordId}/download/pdf",
            Rel = "http://opds-spec.org/acquisition",
            Type = MediaTypeNames.Application.Pdf
        };
        yield break;
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
