using Gallery.Domain.Enums;
using Gallery.Domain.Models;

namespace Gallery.Application.Interfaces;

/// <summary>
/// Result of a library query with pagination info.
/// </summary>
public sealed record QueryResult(IReadOnlyList<MediaItem> Items, int TotalCount);

/// <summary>
/// Result of a grouped library query.
/// </summary>
public sealed record GroupedQueryResult(IReadOnlyList<MediaGroup> Groups, int TotalCount);

/// <summary>
/// Manages indexed media items.
/// </summary>
public interface IMediaItemStore
{
    Task<MediaItem?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<MediaItem?> GetByPathAsync(string path, CancellationToken ct = default);
    Task<IReadOnlyList<MediaItem>> GetAllAsync(int limit = 1000, int offset = 0, CancellationToken ct = default);
    Task<IReadOnlyList<MediaItem>> GetFavoritesAsync(CancellationToken ct = default);

    /// <summary>
    /// Execute a library query with filters, sorting, and pagination.
    /// Returns a flat list of items.
    /// </summary>
    Task<QueryResult> QueryAsync(LibraryQuery query, int limit = 1000, int offset = 0, CancellationToken ct = default);

    /// <summary>
    /// Execute a library query with grouping by date.
    /// Returns items organized into groups (day/month).
    /// </summary>
    Task<GroupedQueryResult> QueryGroupedAsync(LibraryQuery query, int limit = 1000, CancellationToken ct = default);

    Task<long> UpsertAsync(MediaItem item, CancellationToken ct = default);

    /// <summary>
    /// Update an existing media item.
    /// </summary>
    Task UpdateAsync(MediaItem item, CancellationToken ct = default);

    Task UpdateThumbPathAsync(long id, ThumbSize size, string thumbPath, CancellationToken ct = default);
    Task SetFavoriteAsync(long id, bool isFavorite, CancellationToken ct = default);
    Task SetRatingAsync(long id, int rating, CancellationToken ct = default);

    /// <summary>
    /// Delete a media item by ID.
    /// </summary>
    Task DeleteAsync(long id, CancellationToken ct = default);

    Task DeleteByPathAsync(string path, CancellationToken ct = default);
    Task<int> GetCountAsync(CancellationToken ct = default);
}
