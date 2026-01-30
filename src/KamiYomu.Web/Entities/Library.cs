using System.Text.Json;
using System.Xml.Linq;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Entities;

public class Library
{
    private readonly Lazy<LibraryDbContext> _libraryReadWriteDbContext;
    private readonly Lazy<LibraryDbContext> _libraryReadOnlyDbContext;

    protected Library()
    {
        _libraryReadOnlyDbContext = new Lazy<LibraryDbContext>(CreateReadOnlyDbContext);
        _libraryReadWriteDbContext = new Lazy<LibraryDbContext>(CreateReadWriteDbContext);
    }
    public Library(CrawlerAgent agentCrawler, Manga manga, string? filePathTemplate, string? comicInfoTitleTemplateFormat, string? comicInfoSeriesTemplate) : this()
    {
        CrawlerAgent = agentCrawler;
        Manga = string.IsNullOrEmpty(manga.Title) ? null : manga;
        FilePathTemplate = filePathTemplate;
        ComicInfoTitleTemplateFormat = comicInfoTitleTemplateFormat;
        ComicInfoSeriesTemplate = comicInfoSeriesTemplate;
        CreatedDate = DateTimeOffset.UtcNow;
    }

    private LibraryDbContext CreateReadWriteDbContext()
    {
        return new LibraryDbContext(Id, false);
    }

    private LibraryDbContext CreateReadOnlyDbContext()
    {
        return new LibraryDbContext(Id, true);
    }

    public LibraryDbContext GetReadOnlyDbContext()
    {
        return _libraryReadOnlyDbContext.Value;
    }

    public LibraryDbContext GetReadWriteDbContext()
    {
        return _libraryReadWriteDbContext.Value;
    }

    public void DropDbContext()
    {
        _libraryReadWriteDbContext.Value.DropDatabase();
    }

    public string GetDiscovertyJobId()
    {
        return $"{Manga!.Title}-{Id}-{CrawlerAgent.Id}";
    }

    public string GetTempDirectory()
    {
        IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
        string filePathTemplate = FilePathTemplate;

        if (string.IsNullOrWhiteSpace(filePathTemplate))
        {
            filePathTemplate = specialFolderOptions.Value.FilePathFormat;
        }

        string mangaFolder = TemplateResolver.Resolve(filePathTemplate, Manga, null);

        string dirPath = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, Path.GetDirectoryName(mangaFolder));

        if (!Directory.Exists(dirPath))
        {
            _ = Directory.CreateDirectory(dirPath);
        }

        return dirPath;
    }


    public string GetFilePathTemplate()
    {
        string filePathTemplate = FilePathTemplate;
        if (string.IsNullOrWhiteSpace(filePathTemplate))
        {
            IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            filePathTemplate = specialFolderOptions.Value.FilePathFormat;
        }
        return filePathTemplate;
    }

    public string GetComicInfoTitleTemplate()
    {
        string comicInfoTitleTemplate = ComicInfoTitleTemplateFormat;
        if (string.IsNullOrWhiteSpace(comicInfoTitleTemplate))
        {
            IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            comicInfoTitleTemplate = specialFolderOptions.Value.ComicInfoTitleFormat;
        }
        return comicInfoTitleTemplate;
    }

    public string GetComicInfoSeriesTemplate()
    {
        string comicInfoSeriesTemplate = ComicInfoSeriesTemplate;
        if (string.IsNullOrWhiteSpace(comicInfoSeriesTemplate))
        {
            IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            comicInfoSeriesTemplate = specialFolderOptions.Value.ComicInfoSeriesFormat;
        }
        return comicInfoSeriesTemplate;
    }

    public string GetComicInfoSeriesTemplateResolved(Chapter? chapter = null)
    {
        string template = GetComicInfoSeriesTemplate();
        return TemplateResolver.Resolve(template, Manga, chapter);
    }

    public string GetComicInfoTitleTemplateResolved(Chapter? chapter = null)
    {
        string template = GetComicInfoTitleTemplate();
        return TemplateResolver.Resolve(template, Manga, chapter);
    }

    public string GetFilePathTemplateResolved(Chapter? chapter = null)
    {
        string filePathTemplate = GetFilePathTemplate();
        return TemplateResolver.Resolve(filePathTemplate, Manga, chapter);
    }

    public string GetCbzFilePath(Chapter chapter)
    {
        string filePathTemplate = GetFilePathTemplate();
        string filePathTemplateResolved = TemplateResolver.Resolve(filePathTemplate, Manga, chapter);
        IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
        string filePath = Path.Combine(specialFolderOptions.Value.MangaDir, filePathTemplateResolved) + ".cbz";

        string? dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }

        return filePath;
    }

    public string GetCbzFileName(Chapter chapter)
    {
        string cbzFilePath = GetCbzFilePath(chapter);
        string cbzFileName = Path.GetFileName(cbzFilePath);
        return cbzFileName;
    }

    public string GetCbzFileNameWithoutExtension(Chapter chapter)
    {
        string cbzFilePath = GetCbzFilePath(chapter);
        string cbzFileName = Path.GetFileNameWithoutExtension(cbzFilePath);
        return cbzFileName;
    }

    public string GetCbzFileSize(Chapter chapter)
    {
        FileInfo fileInfo = new(GetCbzFilePath(chapter));

        if (!fileInfo.Exists)
        {
            return I18n.NotStarted;
        }

        long bytes = fileInfo.Length;

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        else if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F2} KB";
        }
        else
        {
            return bytes < 1024 * 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0):F2} MB" : $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    public string GetTempChapterDirectory(Chapter chapter)
    {
        string filePathTemplate = GetFilePathTemplate();

        string chapterFolder = TemplateResolver.Resolve(filePathTemplate, Manga, chapter);

        string dirPath = Path.Combine(Path.GetTempPath(), Defaults.Worker.TempDirName, chapterFolder);

        if (!Directory.Exists(dirPath))
        {
            _ = Directory.CreateDirectory(dirPath);
        }

        return dirPath;
    }

    public string GetMangaDirectory()
    {
        IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();

        string dirPath = Path.Combine(specialFolderOptions.Value.MangaDir, GetFilePathTemplateResolved());

        string? directory = Path.GetDirectoryName(dirPath);

        if (!Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        return directory;
    }

    public string ToComicInfo(Chapter chapter)
    {
        string chapterJson = JsonSerializer.Serialize(chapter);
        XElement comicInfo = new("ComicInfo",
            new XElement("Title", $"{GetComicInfoTitleTemplateResolved(chapter)}"),
            new XElement("Series", $"{GetComicInfoSeriesTemplateResolved(chapter)}"),
            new XElement("Number", chapter?.Number.ToString() ?? string.Empty),
            new XElement("Volume", chapter?.Volume.ToString() ?? string.Empty),
            new XElement("Writer", string.Join(", ", chapter?.ParentManga?.Authors ?? [])),
            new XElement("Penciller", string.Join(", ", chapter?.ParentManga?.Artists ?? [])),
            new XElement("CoverArtist", string.Join(", ", chapter?.ParentManga?.Artists ?? [])),
            new XElement("LanguageISO", chapter?.ParentManga?.OriginalLanguage ?? string.Empty),
            new XElement("Genre", string.Join(", ", chapter?.ParentManga?.Tags ?? [])),
            new XElement("ScanInformation", "KamiYomu"),
            new XElement("Web", chapter?.Uri?.ToString() ?? chapter?.ParentManga.WebSiteUrl ?? string.Empty),
            new XElement("AgeRating", (chapter?.ParentManga?.IsFamilySafe ?? true) ? "Everyone" : "Adult"),
            new XElement("Notes", chapterJson)
        );

        return comicInfo.ToString();
    }


    public Guid Id { get; private set; }
    public CrawlerAgent CrawlerAgent { get; private set; }
    public Manga Manga { get; private set; }
    public string? FilePathTemplate { get; private set; }
    public string? ComicInfoTitleTemplateFormat { get; private set; }
    public string? ComicInfoSeriesTemplate { get; private set; }
    public DateTimeOffset CreatedDate { get; private set; } = DateTimeOffset.UtcNow;
}
