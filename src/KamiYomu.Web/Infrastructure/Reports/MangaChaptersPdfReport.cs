using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

using SkiaSharp;

namespace KamiYomu.Web.Infrastructure.Reports;

public class MangaChaptersPdfReport(List<string> images, string fileName, string logoPath) : IDocument
{
    public DocumentMetadata GetMetadata()
    {
        return new()
        {
            Title = $"{fileName} — KamiYomu",
            Author = "KamiYomu",
            Subject = fileName,
            Keywords = string.Join(";", ["Manga", "CBZ", "Comic", "PDF", "KamiYomu"]),
            Creator = "KamiYomu",
            Producer = "KamiYomu"
        };
    }

    public void Compose(IDocumentContainer container)
    {
        string svgMarkup = File.ReadAllText(logoPath);
        if (svgMarkup.Contains("<svg"))
        {
            svgMarkup = svgMarkup.Replace("<svg", "<svg opacity=\"0.25\"");
        }

        foreach (string imgPath in images)
        {
            using SKCodec codec = SKCodec.Create(imgPath);

            int width, height;

            if (codec != null)
            {
                width = codec.Info.Width;
                height = codec.Info.Height;
            }
            else
            {
                using SKBitmap bitmap = SKBitmap.Decode(imgPath);
                if (bitmap == null)
                {
                    continue;
                }

                width = bitmap.Width;
                height = bitmap.Height;
            }

            float widthPoints = width * 72f / 96f;
            float heightPoints = height * 72f / 96f;


            _ = container.Page(page =>
            {
                page.Size(widthPoints, heightPoints);
                page.Margin(0);

                page.Foreground()
                    .Padding(10)
                    .Column(column =>
                    {
                        _ = column.Item()
                            .AlignBottom()
                            .AlignRight()
                            .Width(200).Height(200).ScaleToFit()
                            .Svg(svgMarkup);
                    });


                _ = page.Content()
                    .Image(imgPath)
                    .FitArea();


            });
        }
    }
}
