using Gallery.Domain.Models;

namespace Gallery.App.Services;

/// <summary>
/// Builds a flat list of TimelineRows from grouped media items.
/// This enables virtualized rendering of grouped timeline views.
/// </summary>
public static class TimelineRowBuilder
{
    /// <summary>
    /// Convert groups into a flat list of rows (headers + tile rows).
    /// </summary>
    /// <param name="groups">The media groups to convert.</param>
    /// <param name="columns">Number of columns (tiles per row).</param>
    /// <returns>A flat list of TimelineRow for virtualized rendering.</returns>
    public static IReadOnlyList<TimelineRow> BuildRows(
        IReadOnlyList<MediaGroup> groups,
        int columns)
    {
        if (columns <= 0) columns = 1;

        var rows = new List<TimelineRow>();

        foreach (var group in groups)
        {
            // Add header row
            rows.Add(new GroupHeaderRow(group.Key, group.Title, group.Count));

            // Add tile rows (chunk items by column count)
            var items = group.Items;
            for (int i = 0; i < items.Count; i += columns)
            {
                var slice = items.Skip(i).Take(columns).ToList();
                rows.Add(new TileRow(group.Key, slice));
            }
        }

        return rows;
    }

    /// <summary>
    /// Rebuild rows from existing groups when column count changes.
    /// This is called on window resize without re-fetching from DB.
    /// </summary>
    public static IReadOnlyList<TimelineRow> RebuildRows(
        IReadOnlyList<MediaGroup> groups,
        int newColumns)
    {
        return BuildRows(groups, newColumns);
    }

    /// <summary>
    /// Find the global item index given a row and tile position.
    /// Used for keyboard navigation in grouped mode.
    /// </summary>
    public static int GetGlobalIndex(
        IReadOnlyList<TimelineRow> rows,
        int rowIndex,
        int tileIndex)
    {
        int globalIndex = 0;

        for (int i = 0; i < rowIndex && i < rows.Count; i++)
        {
            if (rows[i] is TileRow tr)
            {
                globalIndex += tr.TileCount;
            }
        }

        if (rowIndex < rows.Count && rows[rowIndex] is TileRow currentRow)
        {
            globalIndex += Math.Min(tileIndex, currentRow.TileCount - 1);
        }

        return globalIndex;
    }

    /// <summary>
    /// Find the row and tile position for a given media item.
    /// Used for scrolling to selection in grouped mode.
    /// </summary>
    public static (int RowIndex, int TileIndex)? FindItemPosition(
        IReadOnlyList<TimelineRow> rows,
        long itemId)
    {
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (rows[rowIndex] is TileRow tr)
            {
                for (int tileIndex = 0; tileIndex < tr.Items.Count; tileIndex++)
                {
                    if (tr.Items[tileIndex].Id == itemId)
                    {
                        return (rowIndex, tileIndex);
                    }
                }
            }
        }

        return null;
    }
}
