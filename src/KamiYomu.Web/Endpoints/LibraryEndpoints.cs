using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;

namespace KamiYomu.Web.Endpoints;

public static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(
        this IEndpointRouteBuilder app)
    {
        _ = app.MapGet("/api/Libraries", (DbContext dbContext, int offset = 0, int limit = 30) =>
        {
            return Results.Ok(dbContext.Libraries.Query().Limit(limit).Offset(offset).ToEnumerable());
        })
        .WithName("GetLibraries")
        .WithTags("Libraries");


        _ = app.MapGet("/api/Libraries/{libraryId:guid}", (DbContext dbContext, Guid libraryId) =>
        {
            return Results.Ok(dbContext.Libraries.FindOne(p => p.Id == libraryId));
        })
        .WithName("GetLibrary")
        .WithTags("Libraries");

        _ = app.MapGet("/api/Libraries/{libraryId:guid}/manga", (DbContext dbContext, Guid libraryId) =>
        {
            Library library = dbContext.Libraries.FindOne(p => p.Id == libraryId);

            if (library == null)
            {
                return Results.Ok(null);
            }

            using LibraryDbContext libContext = library.GetReadOnlyDbContext();

            return Results.Ok(libContext.MangaDownloadRecords.Query().FirstOrDefault());
        })
        .WithName("GetLibraryManga")
        .WithTags("Libraries");

        _ = app.MapGet("/api/Libraries/{libraryId:guid}/manga/chapters", (DbContext dbContext, Guid libraryId, int offset, int limit) =>
        {
            Library library = dbContext.Libraries.FindOne(p => p.Id == libraryId);

            if (library == null)
            {
                return Results.Ok(Enumerable.Empty<ChapterDownloadRecord>());
            }

            using LibraryDbContext libContext = library.GetReadOnlyDbContext();

            return Results.Ok(libContext.ChapterDownloadRecords.Query().Offset(offset).Limit(limit).ToEnumerable());
        })
        .WithName("GetLibraryMangaChapters")
        .WithTags("Libraries");

        return app;
    }
}
