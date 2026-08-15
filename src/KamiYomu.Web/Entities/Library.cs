using System.Text.Json;
using System.Xml.Linq;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services;

using Microsoft.Extensions.Options;

namespace KamiYomu.Web.Entities;
/// <summary>
/// Represents a manga library that manages crawler agent configurations, manga metadata, and database contexts.
/// Provides methods for resolving file paths and templates, managing comic info metadata, and handling CBZ file operations.
/// </summary>
public class Library
{
    private readonly Lazy<LibraryDbContext> _libraryReadWriteDbContext;
    private readonly Lazy<LibraryDbContext> _libraryReadOnlyDbContext;


    /// <summary>
    /// Initializes a new instance of the <see cref="Library"/> class with lazy-initialized database contexts.
    /// </summary>
    protected Library()
    {
        _libraryReadOnlyDbContext = new Lazy<LibraryDbContext>(CreateReadOnlyDbContext);
        _libraryReadWriteDbContext = new Lazy<LibraryDbContext>(CreateReadWriteDbContext);
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="Library"/> class with specified crawler agent and manga information.
    /// </summary>
    /// <param name="agentCrawler">The crawler agent associated with this library.</param>
    /// <param name="manga">The manga entity for this library.</param>
    /// <param name="filePathTemplate">Optional custom file path template for organizing downloaded files.</param>
    /// <param name="comicInfoTitleTemplateFormat">Optional custom template format for comic info title metadata.</param>
    /// <param name="comicInfoSeriesTemplate">Optional custom template for comic info series metadata.</param>
    /// <param name="dailyExecutionTime">Optional custom daily execution time for the library's crawler agent.</param>
    public Library(CrawlerAgent agentCrawler, Manga manga, string? filePathTemplate, string? comicInfoTitleTemplateFormat, string? comicInfoSeriesTemplate, string? dailyExecutionTime) : this()
    {
        CrawlerAgent = agentCrawler;
        Manga = string.IsNullOrEmpty(manga.Title) ? null : manga;
        FilePathTemplate = filePathTemplate;
        ComicInfoTitleTemplateFormat = comicInfoTitleTemplateFormat;
        ComicInfoSeriesTemplate = comicInfoSeriesTemplate;
        DailyExecutionTime = dailyExecutionTime;
        CreatedDate = DateTimeOffset.UtcNow;
    }


    /// <summary>
    /// Updates the manga information for this library, ensuring the manga ID matches the current manga.
    /// </summary>
    /// <param name="manga">The new manga entity with updated information.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manga ID does not match the current library's manga ID.</exception>
    public void UpdateMangaInformation(Manga manga)
    {
        if (Manga?.Id != manga.Id)
        {
            throw new InvalidOperationException($"Cannot update Manga with a different Id. Current Manga Id: {Manga?.Id}, New Manga Id: {manga.Id}");
        }
        Manga = string.IsNullOrEmpty(manga.Title) ? null : manga;
    }

    private LibraryDbContext CreateReadWriteDbContext()
    {
        return new LibraryDbContext(Id, false);
    }

    private LibraryDbContext CreateReadOnlyDbContext()
    {
        return new LibraryDbContext(Id, true);
    }


    /// <summary>
    /// Gets the read-only database context for this library.
    /// </summary>
    /// <returns>A read-only instance of <see cref="LibraryDbContext"/>.</returns>
    public LibraryDbContext GetReadOnlyDbContext()
    {
        return _libraryReadOnlyDbContext.Value;
    }


    /// <summary>
    /// Gets the read-write database context for this library.
    /// </summary>
    /// <returns>A read-write instance of <see cref="LibraryDbContext"/>.</returns>
    public LibraryDbContext GetReadWriteDbContext()
    {
        return _libraryReadWriteDbContext.Value;
    }


    /// <summary>
    /// Drops the database associated with this library's read-write context.
    /// </summary>
    public void DropDbContext()
    {
        _libraryReadWriteDbContext.Value.DropDatabase();
    }


    /// <summary>
    /// Gets a unique discovery job identifier combining manga title, library ID, and crawler agent ID.
    /// </summary>
    /// <returns>A string representing the unique discovery job ID.</returns>
    public string GetDiscovertyJobId()
    {
        return $"{Manga!.Title}-{Id}-{CrawlerAgent.Id}";
    }


    /// <summary>
    /// Gets the temporary directory path for storing downloaded manga files during processing.
    /// Creates the directory if it does not exist.
    /// </summary>
    /// <returns>The full path to the temporary directory.</returns>
    public string GetTempDirectory()
    {
        IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
        string? filePathTemplate = FilePathTemplate;

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

    /// <summary>
    /// Gets the file path template for this library, using custom template if available, otherwise the default from options.
    /// </summary>
    /// <returns>The file path template string.</returns>
    public string GetFilePathTemplate()
    {
        string? filePathTemplate = FilePathTemplate;
        if (string.IsNullOrWhiteSpace(filePathTemplate))
        {
            IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            filePathTemplate = specialFolderOptions.Value.FilePathFormat;
        }
        return filePathTemplate;
    }


    /// <summary>
    /// Gets the comic info title template for this library, using custom template if available, otherwise the default from options.
    /// </summary>
    /// <returns>The comic info title template string.</returns>
    public string GetComicInfoTitleTemplate()
    {
        string? comicInfoTitleTemplate = ComicInfoTitleTemplateFormat;
        if (string.IsNullOrWhiteSpace(comicInfoTitleTemplate))
        {
            IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            comicInfoTitleTemplate = specialFolderOptions.Value.ComicInfoTitleFormat;
        }
        return comicInfoTitleTemplate;
    }


    /// <summary>
    /// Gets the comic info series template for this library, using custom template if available, otherwise the default from options.
    /// </summary>
    /// <returns>The comic info series template string.</returns>
    public string GetComicInfoSeriesTemplate()
    {
        string? comicInfoSeriesTemplate = ComicInfoSeriesTemplate;
        if (string.IsNullOrWhiteSpace(comicInfoSeriesTemplate))
        {
            IOptions<SpecialFolderOptions> specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();
            comicInfoSeriesTemplate = specialFolderOptions.Value.ComicInfoSeriesFormat;
        }
        return comicInfoSeriesTemplate;
    }


    /// <summary>
    /// Resolves the comic info series template with actual values from the specified chapter.
    /// </summary>
    /// <param name="chapter">The chapter to use for template resolution; if null, uses manga-level data.</param>
    /// <param name="keepUnsolvedVariables">If true, keeps unresolved template variables in the output.</param>
    /// <returns>The resolved comic info series string.</returns>
    public string GetComicInfoSeriesTemplateResolved(Chapter? chapter = null, bool keepUnsolvedVariables = false)
    {
        string template = GetComicInfoSeriesTemplate();
        return TemplateResolver.Resolve(template, Manga, chapter, keepUnsolvedVariables: keepUnsolvedVariables);
    }


    /// <summary>
    /// Resolves the comic info title template with actual values from the specified chapter.
    /// </summary>
    /// <param name="chapter">The chapter to use for template resolution; if null, uses manga-level data.</param>
    /// <param name="keepUnsolvedVariables">If true, keeps unresolved template variables in the output.</param>
    /// <returns>The resolved comic info title string.</returns>
    public string GetComicInfoTitleTemplateResolved(Chapter? chapter = null, bool keepUnsolvedVariables = false)
    {
        string template = GetComicInfoTitleTemplate();
        return TemplateResolver.Resolve(template, Manga, chapter, keepUnsolvedVariables: keepUnsolvedVariables);
    }


    /// <summary>
    /// Resolves the file path template with actual values from the specified chapter.
    /// </summary>
    /// <param name="chapter">The chapter to use for template resolution; if null, uses manga-level data.</param>
    /// <param name="keepUnsolvedVariables">If true, keeps unresolved template variables in the output.</param>
    /// <returns>The resolved file path string.</returns>
    public string GetFilePathTemplateResolved(Chapter? chapter = null, bool keepUnsolvedVariables = false)
    {
        string filePathTemplate = GetFilePathTemplate();
        return TemplateResolver.Resolve(filePathTemplate, Manga, chapter, keepUnsolvedVariables: keepUnsolvedVariables);
    }


    /// <summary>
    /// Gets the full file path for a CBZ (Comic Book ZIP) file for the specified chapter.
    /// Creates the directory structure if it does not exist.
    /// </summary>
    /// <param name="chapter">The chapter for which to generate the CBZ file path.</param>
    /// <returns>The full file path with .cbz extension.</returns>
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


    /// <summary>
    /// Gets the file name (including extension) of the CBZ file for the specified chapter.
    /// </summary>
    /// <param name="chapter">The chapter for which to get the CBZ file name.</param>
    /// <returns>The CBZ file name with extension.</returns>
    public string GetCbzFileName(Chapter chapter)
    {
        string cbzFilePath = GetCbzFilePath(chapter);
        string cbzFileName = Path.GetFileName(cbzFilePath);
        return cbzFileName;
    }


    /// <summary>
    /// Gets the file name without extension of the CBZ file for the specified chapter.
    /// </summary>
    /// <param name="chapter">The chapter for which to get the CBZ file name.</param>
    /// <returns>The CBZ file name without extension.</returns>
    public string GetCbzFileNameWithoutExtension(Chapter chapter)
    {
        string cbzFilePath = GetCbzFilePath(chapter);
        string cbzFileName = Path.GetFileNameWithoutExtension(cbzFilePath);
        return cbzFileName;
    }


    /// <summary>
    /// Gets the human-readable file size of the CBZ file for the specified chapter.
    /// Returns "Not started" if the file does not exist.
    /// </summary>
    /// <param name="chapter">The chapter for which to get the file size.</param>
    /// <returns>A formatted file size string (e.g., "1.50 MB") or a localized "Not started" message.</returns>
    public string GetCbzFileSize(Chapter chapter)
    {
        FileInfo fileInfo = new(GetCbzFilePath(chapter));

        if (!fileInfo.Exists)
        {
            return I18n.NotStarted;
        }

        long bytes = fileInfo.Length;

        return bytes < 1024
            ? $"{bytes} B"
            : bytes < 1024 * 1024
                ? $"{bytes / 1024.0:F2} KB"
                : bytes < 1024 * 1024 * 1024
                    ? $"{bytes / (1024.0 * 1024.0):F2} MB"
                    : $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }


    /// <summary>
    /// Gets the temporary directory path for storing a specific chapter's files during processing.
    /// Creates the directory if it does not exist.
    /// </summary>
    /// <param name="chapter">The chapter for which to create the temporary directory.</param>
    /// <returns>The full path to the chapter's temporary directory.</returns>
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


    /// <summary>
    /// Gets the manga directory path based on the file path template and manga information.
    /// Creates the directory if it does not exist.
    /// </summary>
    /// <returns>The full path to the manga directory.</returns>
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


    /// <summary>
    /// Generates an XML ComicInfo metadata string for the specified chapter.
    /// Includes chapter number, volume, authors, artists, language, genre, and serialized chapter data in notes.
    /// </summary>
    /// <param name="chapter">The chapter for which to generate comic info metadata.</param>
    /// <returns>An XML string containing the ComicInfo metadata.</returns>
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

    public void SetCrawlerAgent(CrawlerAgent crawlerAgent)
    {
        if (crawlerAgent.AssemblyName != CrawlerAgent.AssemblyName)
        {
            throw new InvalidOperationException($"Cannot set CrawlerAgent with a different AssemblyName. Current: {CrawlerAgent.AssemblyName}, New: {crawlerAgent.AssemblyName}");
        }
        CrawlerAgent = crawlerAgent;
    }

    /// <summary>
    /// Gets the unique identifier for this library.
    /// </summary>
    public Guid Id { get; private set; }


    /// <summary>
    /// Gets the crawler agent associated with this library.
    /// </summary>
    public CrawlerAgent CrawlerAgent { get; private set; }


    /// <summary>
    /// Gets the manga entity for this library.
    /// </summary>
    public Manga? Manga { get; private set; }


    /// <summary>
    /// Gets the custom file path template for organizing downloaded files, or null if using default.
    /// </summary>
    public string? FilePathTemplate { get; private set; }


    /// <summary>
    /// Gets the custom template format for comic info title metadata, or null if using default.
    /// </summary>
    public string? ComicInfoTitleTemplateFormat { get; private set; }


    /// <summary>
    /// Gets the custom template for comic info series metadata, or null if using default.
    /// </summary>
    public string? ComicInfoSeriesTemplate { get; private set; }


    /// <summary>
    /// Gets the daily execution time for scheduled crawler jobs, if configured.
    /// </summary>
    public string? DailyExecutionTime { get; private set; }


    /// <summary>
    /// Gets the creation date and time (in UTC) of this library.
    /// </summary>
    public DateTimeOffset CreatedDate { get; private set; } = DateTimeOffset.UtcNow;
}
