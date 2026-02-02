using Gallery.Application.Interfaces;
using Gallery.Domain.Enums;
using Gallery.Domain.Models;
using Microsoft.Data.Sqlite;

namespace Gallery.Infrastructure.Data;

public sealed class MediaItemStore : IMediaItemStore
{
    private readonly GalleryDatabase _db;

    public MediaItemStore(GalleryDatabase db)
    {
        _db = db;
    }

    public async Task<MediaItem?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + " WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadItem(reader) : null;
    }

    public async Task<MediaItem?> GetByPathAsync(string path, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + " WHERE path = @path";
        cmd.Parameters.AddWithValue("@path", path);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadItem(reader) : null;
    }

    public async Task<IReadOnlyList<MediaItem>> GetAllAsync(int limit = 1000, int offset = 0, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + " ORDER BY modified_at DESC LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        return await ReadAllAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<MediaItem>> GetFavoritesAsync(CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = SelectColumns + " WHERE is_favorite = 1 ORDER BY modified_at DESC";

        return await ReadAllAsync(cmd, ct);
    }

    /// <summary>
    /// Execute a library query with filters, sorting, and pagination.
    /// All filtering happens in SQLite - never in memory.
    /// </summary>
    public async Task<QueryResult> QueryAsync(LibraryQuery query, int limit = 1000, int offset = 0, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();

        // Build WHERE clause
        var whereClause = BuildWhereClause(query);
        var orderByClause = BuildOrderByClause(query);

        // Get total count
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM items {whereClause}";
        BindQueryParameters(countCmd, query);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // Get items
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectColumns} {whereClause} {orderByClause} LIMIT @limit OFFSET @offset";
        BindQueryParameters(cmd, query);
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        var items = await ReadAllAsync(cmd, ct);
        return new QueryResult(items, totalCount);
    }

    private static string BuildWhereClause(LibraryQuery query)
    {
        var conditions = new List<string> { "1=1" };

        // Text search (filename/path contains)
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            conditions.Add("path LIKE '%' || @text || '%' ESCAPE '\\'");
        }

        // Favorites filter
        if (query.FavoritesOnly)
        {
            conditions.Add("is_favorite = 1");
        }

        // Media type filter
        if (query.MediaType != MediaTypeFilter.All)
        {
            conditions.Add("type = @mediaType");
        }

        return "WHERE " + string.Join(" AND ", conditions);
    }

    private static string BuildOrderByClause(LibraryQuery query)
    {
        var direction = query.SortDir == SortDir.Desc ? "DESC" : "ASC";

        var orderBy = query.SortBy switch
        {
            SortField.TakenAt => $"COALESCE(taken_at, modified_at) {direction}, path ASC",
            SortField.ModifiedAt => $"modified_at {direction}, path ASC",
            SortField.Size => $"size_bytes {direction}, path ASC",
            SortField.Name => $"path {direction}",
            _ => $"modified_at {direction}, path ASC"
        };

        return $"ORDER BY {orderBy}";
    }

    private static void BindQueryParameters(SqliteCommand cmd, LibraryQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            // Escape LIKE wildcards so user text is literal
            var escapedText = query.Text.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            cmd.Parameters.AddWithValue("@text", escapedText);
        }

        if (query.MediaType != MediaTypeFilter.All)
        {
            // Map filter enum to domain enum
            var mediaType = query.MediaType == MediaTypeFilter.Images
                ? (int)MediaType.Image
                : (int)MediaType.Video;
            cmd.Parameters.AddWithValue("@mediaType", mediaType);
        }
    }

    /// <summary>
    /// Execute a grouped library query for timeline display.
    /// Groups items by day or month, using taken_at when available, otherwise modified_at.
    /// </summary>
    public async Task<GroupedQueryResult> QueryGroupedAsync(LibraryQuery query, int limit = 1000, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();

        var whereClause = BuildWhereClause(query);
        var orderByClause = BuildOrderByClause(query);

        // Get total count
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM items {whereClause}";
        BindQueryParameters(countCmd, query);
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // Get items (limited)
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectColumns} {whereClause} {orderByClause} LIMIT @limit";
        BindQueryParameters(cmd, query);
        cmd.Parameters.AddWithValue("@limit", limit);

        var items = await ReadAllAsync(cmd, ct);

        // Group in memory (SQLite doesn't support complex grouping easily)
        var groups = GroupItems(items, query.GroupBy);

        return new GroupedQueryResult(groups, totalCount);
    }

    private static IReadOnlyList<MediaGroup> GroupItems(IReadOnlyList<MediaItem> items, GroupBy groupBy)
    {
        if (groupBy == GroupBy.None || items.Count == 0)
        {
            // Return single group with all items
            return new[]
            {
                new MediaGroup
                {
                    Key = "all",
                    Title = "All Items",
                    Items = items
                }
            };
        }

        // Group by effective date (taken_at or modified_at)
        var grouped = items
            .GroupBy(item =>
            {
                var date = item.TakenAt ?? item.ModifiedAt;
                return groupBy == GroupBy.Day
                    ? date.ToString("yyyy-MM-dd")
                    : date.ToString("yyyy-MM");
            })
            .Select(g => new MediaGroup
            {
                Key = g.Key,
                Title = FormatGroupTitle(g.Key, groupBy),
                Items = g.ToList()
            })
            .ToList();

        return grouped;
    }

    private static string FormatGroupTitle(string key, GroupBy groupBy)
    {
        // Parse the key and format nicely
        if (groupBy == GroupBy.Day && DateOnly.TryParse(key, out var day))
        {
            return day.ToString("MMMM d, yyyy"); // "June 15, 2026"
        }

        if (groupBy == GroupBy.Month && key.Length == 7) // "yyyy-MM"
        {
            var parts = key.Split('-');
            if (int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month))
            {
                var date = new DateOnly(year, month, 1);
                return date.ToString("MMMM yyyy"); // "June 2026"
            }
        }

        return key;
    }

    public async Task<long> UpsertAsync(MediaItem item, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO items (path, extension, type, size_bytes, modified_at, taken_at, width, height, duration_ticks, is_favorite, rating, thumb_small_path, thumb_large_path, last_indexed_at)
            VALUES (@path, @extension, @type, @size_bytes, @modified_at, @taken_at, @width, @height, @duration_ticks, @is_favorite, @rating, @thumb_small_path, @thumb_large_path, @last_indexed_at)
            ON CONFLICT(path) DO UPDATE SET
                extension = excluded.extension,
                type = excluded.type,
                size_bytes = excluded.size_bytes,
                modified_at = excluded.modified_at,
                taken_at = excluded.taken_at,
                width = excluded.width,
                height = excluded.height,
                duration_ticks = excluded.duration_ticks,
                last_indexed_at = excluded.last_indexed_at
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("@path", item.Path);
        cmd.Parameters.AddWithValue("@extension", item.Extension);
        cmd.Parameters.AddWithValue("@type", (int)item.Type);
        cmd.Parameters.AddWithValue("@size_bytes", item.SizeBytes);
        cmd.Parameters.AddWithValue("@modified_at", item.ModifiedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@taken_at", item.TakenAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@width", item.Width ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@height", item.Height ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@duration_ticks", item.Duration?.Ticks ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@is_favorite", item.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@rating", item.Rating);
        cmd.Parameters.AddWithValue("@thumb_small_path", item.ThumbSmallPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@thumb_large_path", item.ThumbLargePath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@last_indexed_at", item.LastIndexedAt.ToString("o"));

        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateThumbPathAsync(long id, ThumbSize size, string thumbPath, CancellationToken ct = default)
    {
        var column = size == ThumbSize.Small ? "thumb_small_path" : "thumb_large_path";
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE items SET {column} = @thumb_path WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@thumb_path", thumbPath);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetFavoriteAsync(long id, bool isFavorite, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE items SET is_favorite = @is_favorite WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@is_favorite", isFavorite ? 1 : 0);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetRatingAsync(long id, int rating, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE items SET rating = @rating WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@rating", Math.Clamp(rating, 0, 5));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(MediaItem item, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE items SET
                path = @path,
                extension = @extension,
                type = @type,
                size_bytes = @size_bytes,
                modified_at = @modified_at,
                taken_at = @taken_at,
                width = @width,
                height = @height,
                duration_ticks = @duration_ticks,
                is_favorite = @is_favorite,
                rating = @rating,
                thumb_small_path = @thumb_small_path,
                thumb_large_path = @thumb_large_path,
                last_indexed_at = @last_indexed_at
            WHERE id = @id
            """;

        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@path", item.Path);
        cmd.Parameters.AddWithValue("@extension", item.Extension);
        cmd.Parameters.AddWithValue("@type", (int)item.Type);
        cmd.Parameters.AddWithValue("@size_bytes", item.SizeBytes);
        cmd.Parameters.AddWithValue("@modified_at", item.ModifiedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@taken_at", item.TakenAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@width", item.Width ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@height", item.Height ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@duration_ticks", item.Duration?.Ticks ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@is_favorite", item.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@rating", item.Rating);
        cmd.Parameters.AddWithValue("@thumb_small_path", item.ThumbSmallPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@thumb_large_path", item.ThumbLargePath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@last_indexed_at", item.LastIndexedAt.ToString("o"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteByPathAsync(string path, CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE path = @path";
        cmd.Parameters.AddWithValue("@path", path);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        var conn = _db.GetConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private const string SelectColumns = """
        SELECT id, path, extension, type, size_bytes, modified_at, taken_at, width, height, duration_ticks, is_favorite, rating, thumb_small_path, thumb_large_path, last_indexed_at
        FROM items
        """;

    private static async Task<IReadOnlyList<MediaItem>> ReadAllAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var results = new List<MediaItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadItem(reader));
        }
        return results;
    }

    private static MediaItem ReadItem(SqliteDataReader reader)
    {
        return new MediaItem
        {
            Id = reader.GetInt64(0),
            Path = reader.GetString(1),
            Extension = reader.GetString(2),
            Type = (MediaType)reader.GetInt32(3),
            SizeBytes = reader.GetInt64(4),
            ModifiedAt = DateTimeOffset.Parse(reader.GetString(5)),
            TakenAt = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
            Width = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            Height = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            Duration = reader.IsDBNull(9) ? null : TimeSpan.FromTicks(reader.GetInt64(9)),
            IsFavorite = reader.GetInt32(10) == 1,
            Rating = reader.GetInt32(11),
            ThumbSmallPath = reader.IsDBNull(12) ? null : reader.GetString(12),
            ThumbLargePath = reader.IsDBNull(13) ? null : reader.GetString(13),
            LastIndexedAt = DateTimeOffset.Parse(reader.GetString(14))
        };
    }
}
