using Gallery.Domain.Models;

namespace Gallery.App.Services;

/// <summary>
/// Single source of truth for item selection across all views.
/// Grid, inspector, viewer, filmstrip all listen to this.
/// </summary>
public sealed class SelectionService
{
    private MediaItem? _selectedItem;
    private int _selectedIndex = -1;
    private IReadOnlyList<MediaItem> _items = [];

    public MediaItem? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (_selectedItem?.Id == value?.Id) return;
            var previous = _selectedItem;
            _selectedItem = value;
            SelectionChanged?.Invoke(this, new ItemSelectionChangedEventArgs(previous, value, _selectedIndex));
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        private set => _selectedIndex = value;
    }

    public IReadOnlyList<MediaItem> Items
    {
        get => _items;
        set
        {
            _items = value ?? [];
            // Preserve selection if item still exists
            if (_selectedItem is not null)
            {
                var newIndex = _items.ToList().FindIndex(i => i.Id == _selectedItem.Id);
                if (newIndex >= 0)
                {
                    _selectedIndex = newIndex;
                    // Update the item reference in case it changed
                    SelectedItem = _items[newIndex];
                }
                else
                {
                    // Item no longer in list
                    _selectedIndex = -1;
                    SelectedItem = null;
                }
            }
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int Count => _items.Count;

    public event EventHandler<ItemSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler? ItemsChanged;

    /// <summary>
    /// Select an item by reference.
    /// </summary>
    public void Select(MediaItem? item)
    {
        if (item is null)
        {
            _selectedIndex = -1;
            SelectedItem = null;
            return;
        }

        var index = _items.ToList().FindIndex(i => i.Id == item.Id);
        if (index >= 0)
        {
            _selectedIndex = index;
            SelectedItem = _items[index];
        }
    }

    /// <summary>
    /// Select by index.
    /// </summary>
    public void SelectAt(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            _selectedIndex = -1;
            SelectedItem = null;
            return;
        }

        _selectedIndex = index;
        SelectedItem = _items[index];
    }

    /// <summary>
    /// Move selection by delta. Clamps to bounds.
    /// </summary>
    public void Move(int delta)
    {
        if (_items.Count == 0) return;

        var newIndex = _selectedIndex + delta;
        newIndex = Math.Clamp(newIndex, 0, _items.Count - 1);
        SelectAt(newIndex);
    }

    /// <summary>
    /// Move by row (for grid navigation).
    /// </summary>
    public void MoveRow(int rowDelta, int columnsPerRow)
    {
        Move(rowDelta * columnsPerRow);
    }

    /// <summary>
    /// Jump to start.
    /// </summary>
    public void SelectFirst()
    {
        if (_items.Count > 0)
        {
            SelectAt(0);
        }
    }

    /// <summary>
    /// Jump to end.
    /// </summary>
    public void SelectLast()
    {
        if (_items.Count > 0)
        {
            SelectAt(_items.Count - 1);
        }
    }

    /// <summary>
    /// Move to start of current row.
    /// </summary>
    public void SelectRowStart(int columnsPerRow)
    {
        if (_selectedIndex < 0) return;
        var rowStart = (_selectedIndex / columnsPerRow) * columnsPerRow;
        SelectAt(rowStart);
    }

    /// <summary>
    /// Move to end of current row.
    /// </summary>
    public void SelectRowEnd(int columnsPerRow)
    {
        if (_selectedIndex < 0) return;
        var rowStart = (_selectedIndex / columnsPerRow) * columnsPerRow;
        var rowEnd = Math.Min(rowStart + columnsPerRow - 1, _items.Count - 1);
        SelectAt(rowEnd);
    }

    /// <summary>
    /// Page up/down by viewport.
    /// </summary>
    public void PageMove(int direction, int itemsPerPage)
    {
        Move(direction * itemsPerPage);
    }

    /// <summary>
    /// Get adjacent items for prefetching.
    /// </summary>
    public IEnumerable<MediaItem> GetAdjacentItems(int range = 2)
    {
        if (_selectedIndex < 0) yield break;

        for (var i = 1; i <= range; i++)
        {
            var prev = _selectedIndex - i;
            var next = _selectedIndex + i;

            if (next < _items.Count)
                yield return _items[next];
            if (prev >= 0)
                yield return _items[prev];
        }
    }

    /// <summary>
    /// Update an item in the list (e.g., when thumb path changes).
    /// </summary>
    public void UpdateItem(MediaItem updated)
    {
        var list = _items.ToList();
        var index = list.FindIndex(i => i.Id == updated.Id);
        if (index >= 0)
        {
            list[index] = updated;
            _items = list;

            // Update selected item if it's the one that changed
            if (_selectedItem?.Id == updated.Id)
            {
                _selectedItem = updated;
                // Don't fire SelectionChanged, just update the reference
            }
        }
    }
}

public sealed class ItemSelectionChangedEventArgs : EventArgs
{
    public MediaItem? Previous { get; }
    public MediaItem? Current { get; }
    public int CurrentIndex { get; }

    public ItemSelectionChangedEventArgs(MediaItem? previous, MediaItem? current, int currentIndex)
    {
        Previous = previous;
        Current = current;
        CurrentIndex = currentIndex;
    }
}
