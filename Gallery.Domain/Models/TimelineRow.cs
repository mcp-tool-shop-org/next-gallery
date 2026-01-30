namespace Gallery.Domain.Models;

/// <summary>
/// Base type for virtualized timeline rows.
/// A timeline is rendered as a flat list of rows (headers and tile rows).
/// </summary>
public abstract record TimelineRow;

/// <summary>
/// A group header row showing the date/month title and item count.
/// </summary>
public sealed record GroupHeaderRow(
    string Key,
    string Title,
    int Count
) : TimelineRow;

/// <summary>
/// A row of media tiles (up to N items based on column count).
/// </summary>
public sealed record TileRow(
    string GroupKey,
    IReadOnlyList<MediaItem> Items
) : TimelineRow
{
    /// <summary>
    /// Number of tiles in this row (may be less than column count for last row).
    /// </summary>
    public int TileCount => Items.Count;
}
