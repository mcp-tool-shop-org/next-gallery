using Gallery.Application.Interfaces;
using Gallery.Domain.Enums;
using Gallery.Domain.Models;

namespace Gallery.App.Services;

/// <summary>
/// Prefetches large preview thumbnails for adjacent items.
/// This is what makes browsing feel "instant" even while thumbs generate.
/// </summary>
public sealed class PrefetchService : IDisposable
{
    private readonly SelectionService _selection;
    private readonly IThumbJobStore _jobStore;
    private readonly IMediaItemStore _itemStore;
    private readonly IThumbCache _cache;

    private CancellationTokenSource? _prefetchCts;
    private readonly int _prefetchRange;

    public PrefetchService(
        SelectionService selection,
        IThumbJobStore jobStore,
        IMediaItemStore itemStore,
        IThumbCache cache,
        int prefetchRange = 3)
    {
        _selection = selection;
        _jobStore = jobStore;
        _itemStore = itemStore;
        _cache = cache;
        _prefetchRange = prefetchRange;

        _selection.SelectionChanged += OnSelectionChanged;
    }

    private async void OnSelectionChanged(object? sender, ItemSelectionChangedEventArgs e)
    {
        // Cancel any pending prefetch
        _prefetchCts?.Cancel();
        _prefetchCts = new CancellationTokenSource();
        var ct = _prefetchCts.Token;

        try
        {
            // Small delay to avoid thrashing when arrowing quickly
            await Task.Delay(50, ct);

            await PrefetchAdjacentAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Expected when selection changes rapidly
        }
    }

    private async Task PrefetchAdjacentAsync(CancellationToken ct)
    {
        var current = _selection.SelectedItem;
        if (current is null) return;

        // Ensure current item's large thumb is high priority
        await EnsureLargeThumbAsync(current, priority: 100, ct);

        // Prefetch adjacent items
        var adjacent = _selection.GetAdjacentItems(_prefetchRange).ToList();
        var priority = 50;

        foreach (var item in adjacent)
        {
            ct.ThrowIfCancellationRequested();
            await EnsureLargeThumbAsync(item, priority--, ct);
        }
    }

    private async Task EnsureLargeThumbAsync(MediaItem item, int priority, CancellationToken ct)
    {
        // Check if large thumb already exists
        if (!string.IsNullOrEmpty(item.ThumbLargePath) && _cache.Exists(item.ThumbLargePath))
        {
            return;
        }

        // Enqueue high-priority job
        await _jobStore.EnqueueAsync(item.Id, ThumbSize.Large, priority, ct);
    }

    public void Dispose()
    {
        _selection.SelectionChanged -= OnSelectionChanged;
        _prefetchCts?.Cancel();
        _prefetchCts?.Dispose();
    }
}
