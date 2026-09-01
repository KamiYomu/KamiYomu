using KamiYomu.CrawlerAgents.Core.Catalog;
using KamiYomu.Web.Entities.CrawlerAgentRuntime.Interfaces;

namespace KamiYomu.Web.Entities.CrawlerAgentRuntime;
/// <summary>
/// CrawlerAgentDecorator is a decorator class that wraps an instance of ICrawlerAgent, providing additional functionality while maintaining the original interface. It implements the ICrawlerAgentDecorator interface, which combines the functionalities of ICrawlerAgent and IDefaultHeadersCrawlerAgent, along with IDisposable for resource management.
/// </summary>
/// <param name="inner"></param>
public class CrawlerAgentDecorator(ICrawlerAgent inner) : ICrawlerAgentDecorator
{
    private readonly ICrawlerAgent _inner = inner;

    /// <inheritdoc/>
    public async Task<Uri> GetFaviconAsync(CancellationToken cancellationToken = default)
    {
        return await _inner.GetFaviconAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Manga> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _inner.GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PagedResult<Manga>> SearchAsync(string titleName, PaginationOptions paginationOptions, CancellationToken cancellationToken)
    {
        return _inner.SearchAsync(titleName, paginationOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PagedResult<Chapter>> GetChaptersAsync(Manga manga, PaginationOptions paginationOptions, CancellationToken cancellationToken)
    {
        return _inner.GetChaptersAsync(manga, paginationOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IEnumerable<Page>> GetChapterPagesAsync(Chapter chapter, CancellationToken cancellationToken = default)
    {
        return _inner.GetChapterPagesAsync(chapter, cancellationToken);
    }

    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, string>> GetDefaultHeaders()
    {
        return _inner is IDefaultHeadersCrawlerAgent downloadHeaders ? downloadHeaders.GetDefaultHeaders() : [];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _inner.Dispose();
        GC.SuppressFinalize(this);
    }
}
