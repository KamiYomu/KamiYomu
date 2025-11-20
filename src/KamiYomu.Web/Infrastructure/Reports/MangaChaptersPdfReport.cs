using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace KamiYomu.Web.Infrastructure.Reports;

public class MangaChaptersPdfReport(List<string> images, string fileName, string logoPath) : IDocument
{
    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{fileName} — KamiYomu",
        Author = "KamiYomu",
        Subject = fileName,
        Keywords = string.Join(";", ["Manga", "CBZ", "Comic", "PDF", "KamiYomu"]),
        Creator = "KamiYomu",
        Producer = "KamiYomu"
    };

    public void Compose(IDocumentContainer container)
    {
        var svgMarkup = File.ReadAllText(logoPath).Replace("fill=\"#000000\"", "fill=\"#808080\"");

        foreach (var imgPath in images)
        {
            using var codec = SKCodec.Create(imgPath);
            var info = codec.Info;

            float widthPoints = info.Width * 72f / 96f;
            float heightPoints = info.Height * 72f / 96f;

            container.Page(page =>
            {
                page.Size(widthPoints, heightPoints);
                page.Margin(0);

                // Always draw watermark
                page.Foreground()
                    .AlignTop()
                    .AlignRight()
                    .Width(40)
                    .Height(40)
                    .Svg(svgMarkup);

                // Image content
                page.Content()
                    .Image(imgPath)
                    .FitArea();

                // Footer
                page.Footer()
                    .AlignRight()
                    .Text($"KamiYomu - {fileName}")
                    .FontSize(10)
                    .Italic()
                    .FontColor(Colors.Grey.Medium);
            });
        }
    }
}