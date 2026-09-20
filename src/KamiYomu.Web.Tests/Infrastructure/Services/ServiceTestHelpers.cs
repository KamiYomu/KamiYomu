using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.CrawlerAgents.Core.Catalog.Builders;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Definitions;
using KamiYomu.Web.Entities.Integrations;
using KamiYomu.Web.Infrastructure.Contexts;
using KamiYomu.Web.Infrastructure.Services.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using MonkeyCache;
using MonkeyCache.LiteDB;

namespace KamiYomu.Web.Tests.Infrastructure.Services;

internal static class ServiceTestHelpers
{
    private static readonly object SyncRoot = new();
    private static bool _serviceLocatorConfigured;
    private const string OnePixelPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO7Z0ioAAAAASUVORK5CYII=";

    public static string ArtifactsRoot => Path.Combine(AppContext.BaseDirectory, "ServiceTestArtifacts");

    public static void EnsureServiceLocatorConfigured()
    {
        lock (SyncRoot)
        {
            if (_serviceLocatorConfigured)
            {
                return;
            }

            _ = Directory.CreateDirectory(ArtifactsRoot);

            // Production sets this at startup (WebHostings), which tests never run.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            ServiceCollection services = new();
            _ = services.AddSingleton<IOptions<SpecialFolderOptions>>(Options.Create(new SpecialFolderOptions
            {
                MangaDir = Path.Combine(ArtifactsRoot, "manga"),
                AgentsDir = Path.Combine(ArtifactsRoot, "agents"),
                DbDir = Path.Combine(ArtifactsRoot, "db"),
                LogDir = Path.Combine(ArtifactsRoot, "logs"),
                FilePathFormat = "{manga_title}/chapter-{chapter_padded_4}",
                ComicInfoTitleFormat = "{manga_title} ch.{chapter_padded_4}",
                ComicInfoSeriesFormat = "{manga_title}"
            }));

            _ = services.AddLogging();
            _ = services.AddSingleton<ILockManager, NoOpLockManager>();

            // ASSUMPTION: some Worker attributes (e.g. ChapterCancelOnFailAttribute) resolve
            // DbContext from a service scope created off Defaults.ServiceLocator.Instance, the
            // same way production does via AddScoped(_ => new DbContext(...)) in
            // StorageHostings.cs. Since ServiceLocator.Instance is a process-wide singleton whose
            // backing provider can't be swapped per-test (see TestAssemblyInitializer), tests
            // instead flow the DbContext to use for the current logical call context through
            // AmbientDbContext.Current before creating a scope/resolving DbContext.
            _ = services.AddScoped<DbContext>(_ => AmbientDbContext.Current
                ?? throw new InvalidOperationException(
                    "No DbContext configured for ServiceLocator resolution. Set ServiceTestHelpers.AmbientDbContext.Current before resolving DbContext through a ServiceLocator-created scope."));

            ServiceProvider provider = services.BuildServiceProvider();
            Defaults.ServiceLocator.Configure(() => provider);
            _serviceLocatorConfigured = true;
        }
    }

    /// <summary>
    /// Lets tests provide the <see cref="DbContext"/> instance that code resolving DbContext via
    /// <see cref="Defaults.ServiceLocator"/> (e.g. Worker attribute filters) should observe for the
    /// current logical call context. Backed by <see cref="AsyncLocal{T}"/> so parallel test classes
    /// don't interfere with one another.
    /// </summary>
    internal static class AmbientDbContext
    {
        private static readonly AsyncLocal<DbContext?> _current = new();

        public static DbContext? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }
    }

    /// <summary>
    /// Lets tests override the lock-acquisition behavior of the process-wide <see cref="ILockManager"/>
    /// singleton registered in the shared ServiceLocator provider. Backed by <see cref="AsyncLocal{T}"/>
    /// so parallel test classes don't interfere with one another; defaults to always granting a lock
    /// (matching the original NoOpLockManager behavior) when no override is set.
    /// </summary>
    internal static class LockManagerTestHook
    {
        private static readonly AsyncLocal<Func<string, IDisposable?>?> _override = new();

        public static Func<string, IDisposable?>? Override
        {
            get => _override.Value;
            set => _override.Value = value;
        }
    }

    private sealed class NoOpLockManager : ILockManager
    {
        public IDisposable? TryAcquireAsync(string crawlerId) =>
            LockManagerTestHook.Override?.Invoke(crawlerId) ?? new NoOpHandle();

        private sealed class NoOpHandle : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    public static void InitializeCache(string scope)
    {
        Barrel.ApplicationId = $"KamiYomu.Web.Tests.{scope}.{Guid.NewGuid():N}";
        new CacheContext().EmptyAll();
    }

    public static Library CreateLibrary(string? title = null, string? filePathTemplate = null)
    {
        EnsureServiceLocatorConfigured();

        Manga manga = MangaBuilder.Create()
            .WithTitle(title ?? $"Test Manga {Guid.NewGuid():N}")
            .WithOriginalLanguage("ja")
            .WithTags(["action", "test"])
            .WithCoverUrl(new Uri("https://example.com/cover.png"))
            .WithWebsiteUrl("https://example.com/manga")
            .WithIsFamilySafe(true)
            .Build();

        Library library = new(
            new CrawlerAgent("Test.Agent.dll", "Test Agent", new Dictionary<string, object>()),
            manga,
            filePathTemplate ?? "{manga_title}/chapter-{chapter_padded_4}",
            "{manga_title} ch.{chapter_padded_4}",
            "{manga_title}");

        SetProperty(library, nameof(Library.Id), Guid.NewGuid());
        SetProperty(library.CrawlerAgent, nameof(CrawlerAgent.Id), Guid.NewGuid());

        return library;
    }

    /// <summary>
    /// Builds a <see cref="Library"/> backed by the given <see cref="CrawlerAgent"/> instance (rather than a
    /// freshly generated one), so callers can create a library that shares the exact same crawler agent
    /// (and Id) as one they've already inserted into a <see cref="DbContext"/> elsewhere in the test.
    /// </summary>
    public static Library CreateLibrary(CrawlerAgent crawlerAgent, string? title = null, string? filePathTemplate = null)
    {
        EnsureServiceLocatorConfigured();

        Manga manga = MangaBuilder.Create()
            .WithTitle(title ?? $"Test Manga {Guid.NewGuid():N}")
            .WithOriginalLanguage("ja")
            .WithTags(["action", "test"])
            .WithCoverUrl(new Uri("https://example.com/cover.png"))
            .WithWebsiteUrl("https://example.com/manga")
            .WithIsFamilySafe(true)
            .Build();

        Library library = new(
            crawlerAgent,
            manga,
            filePathTemplate ?? "{manga_title}/chapter-{chapter_padded_4}",
            "{manga_title} ch.{chapter_padded_4}",
            "{manga_title}");

        SetProperty(library, nameof(Library.Id), Guid.NewGuid());

        return library;
    }

    public static Chapter CreateChapter(decimal number = 1, string title = "Chapter 1")
    {
        return ChapterBuilder.Create()
            .WithNumber(number)
            .WithVolume(1)
            .WithTitle(title)
            .WithUri(new Uri($"https://example.com/chapters/{number}", UriKind.Absolute))
            .Build();
    }

    public static StoredLibraryRecord CreateCompletedStoredChapter(DbContext dbContext, decimal chapterNumber = 1, string chapterTitle = "Chapter 1")
    {
        Library library = CreateLibrary();
        _ = dbContext.Libraries.Insert(library);

        Chapter chapter = ChapterBuilder.Create(CreateChapter(chapterNumber, chapterTitle))
            .WithParentManga(library.Manga)
            .Build();

        MangaDownloadRecord mangaDownload = new(library, "manga-job-1");
        SetProperty(mangaDownload, nameof(MangaDownloadRecord.Id), Guid.NewGuid());

        ChapterDownloadRecord chapterDownload = new(library.CrawlerAgent, mangaDownload, chapter);
        SetProperty(chapterDownload, nameof(ChapterDownloadRecord.Id), Guid.NewGuid());
        chapterDownload.Complete();

        using LibraryDbContext libraryDbContext = library.GetReadWriteDbContext();
        _ = libraryDbContext.MangaDownloadRecords.Insert(mangaDownload);
        _ = libraryDbContext.ChapterDownloadRecords.Insert(chapterDownload);

        return new StoredLibraryRecord(library, mangaDownload, chapterDownload, chapter);
    }

    public static string CreateCbzFile(Library library, Chapter chapter, params string[] entryNames)
    {
        string filePath = library.GetCbzFilePath(chapter);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using ZipArchive archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
        foreach (string entryName in entryNames.Length > 0 ? entryNames : ["001.png"])
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using Stream entryStream = entry.Open();
            byte[] imageBytes = Convert.FromBase64String(OnePixelPngBase64);
            entryStream.Write(imageBytes, 0, imageBytes.Length);
        }

        return filePath;
    }

    public static void CleanupLibraryArtifacts(Library library, Chapter? chapter = null)
    {
        try
        {
            library.DropDbContext();
        }
        catch
        {
        }

        if (chapter != null)
        {
            try
            {
                string? directory = Path.GetDirectoryName(library.GetCbzFilePath(chapter));
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    public static UserPreference CreateUserPreferenceWithGotify(GotifySettings gotifySettings)
    {
        UserPreference preference = new(CultureInfo.GetCultureInfo("en-US"));
        preference.SetGotifySettings(gotifySettings);
        return preference;
    }

    public static UserPreference CreateUserPreferenceWithKavita(KavitaSettings kavitaSettings)
    {
        UserPreference preference = new(CultureInfo.GetCultureInfo("en-US"));
        preference.SetKavitaSettings(kavitaSettings);
        return preference;
    }

    public static void AssignId(object target)
    {
        SetProperty(target, "Id", Guid.NewGuid());
    }

    /// <summary>
    /// Builds a <see cref="PageContext"/> backed by a real <see cref="DefaultHttpContext"/> whose
    /// RequestServices resolve a no-op <see cref="ITempDataDictionaryFactory"/>. PageModel.Partial()/
    /// TempData access otherwise throws a NullReferenceException when RequestServices is unset.
    /// </summary>
    public static PageContext CreatePageContext(Action<DefaultHttpContext>? configure = null)
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<ITempDataProvider, NoOpTempDataProvider>();
        _ = services.AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>();
        _ = services.AddSingleton<IModelMetadataProvider, EmptyModelMetadataProvider>();
        ServiceProvider provider = services.BuildServiceProvider();

        DefaultHttpContext httpContext = new()
        {
            RequestServices = provider
        };
        configure?.Invoke(httpContext);

        // PageModel.ViewData forwards to PageContext.ViewData rather than lazily creating one;
        // the real Razor Pages engine populates this during page activation, which a direct
        // `new SomePageModel(...)` in a unit test bypasses.
        return new PageContext
        {
            HttpContext = httpContext,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new EmptyModelMetadataProvider(),
                new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary())
        };
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        target.GetType()
            .GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(target, value);
    }
}

internal sealed record StoredLibraryRecord(
    Library Library,
    MangaDownloadRecord MangaDownload,
    ChapterDownloadRecord ChapterDownload,
    Chapter Chapter);

internal sealed record RecordedHttpRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string Body,
    IReadOnlyDictionary<string, string[]> Headers);

internal sealed class RecordingHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    public List<RecordedHttpRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content == null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedHttpRequest(
            request.Method,
            request.RequestUri,
            body,
            request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase)));

        return await responder(request, cancellationToken);
    }
}

internal sealed class StubHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => httpClient;
}

internal sealed class LocalHttpServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, Func<HttpListenerRequest, HttpResponseData>> _routes;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _serverTask;

    public LocalHttpServer(Dictionary<string, Func<HttpListenerRequest, HttpResponseData>> routes)
    {
        _routes = routes;

        int port = GetFreePort();
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add(BaseUri.ToString());
        _listener.Start();
        _serverTask = Task.Run(ListenAsync);
    }

    public Uri BaseUri { get; }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();

        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch
        {
        }

        try
        {
            _serverTask.GetAwaiter().GetResult();
        }
        catch
        {
        }

        _cancellationTokenSource.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch when (_cancellationTokenSource.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (context == null)
            {
                continue;
            }

            HttpResponseData responseData = _routes.TryGetValue(context.Request.RawUrl ?? "/", out var handler)
                ? handler(context.Request)
                : HttpResponseData.Text("Not Found", statusCode: HttpStatusCode.NotFound, contentType: "text/plain");

            context.Response.StatusCode = (int)responseData.StatusCode;
            context.Response.ContentType = responseData.ContentType;

            foreach ((string key, string value) in responseData.Headers)
            {
                context.Response.Headers[key] = value;
            }

            await context.Response.OutputStream.WriteAsync(responseData.Body);
            context.Response.Close();
        }
    }

    private static int GetFreePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed record HttpResponseData(
    HttpStatusCode StatusCode,
    string ContentType,
    byte[] Body,
    IReadOnlyDictionary<string, string> Headers)
{
    public static HttpResponseData Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseData(
            statusCode,
            "application/json",
            Encoding.UTF8.GetBytes(json),
            new Dictionary<string, string>());
    }

    public static HttpResponseData Bytes(
        byte[] body,
        string contentType = "application/octet-stream",
        HttpStatusCode statusCode = HttpStatusCode.OK,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return new HttpResponseData(
            statusCode,
            contentType,
            body,
            headers ?? new Dictionary<string, string>());
    }

    public static HttpResponseData Text(string text, HttpStatusCode statusCode, string contentType)
    {
        return new HttpResponseData(
            statusCode,
            contentType,
            Encoding.UTF8.GetBytes(text),
            new Dictionary<string, string>());
    }
}
