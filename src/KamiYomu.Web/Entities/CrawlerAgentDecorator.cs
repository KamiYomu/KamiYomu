using KamiYomu.CrawlerAgents.Core.Catalog;

namespace KamiYomu.Web.Entities
{
    public class CrawlerAgentDecorator(ICrawlerAgent inner) : ICrawlerAgent
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
        public void Dispose()
        {
            _inner.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
