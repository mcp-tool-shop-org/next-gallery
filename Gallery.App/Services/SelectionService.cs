using Gallery.Domain.Models;

namespace Gallery.App.Services;

/// <summary>
/// Single source of truth for item selection across all views.
/// Supports both single and multi-select modes.
/// Grid, inspector, viewer, filmstrip all listen to this.
/// </summary>
public sealed class SelectionService
{
    private MediaItem? _selectedItem;
    private int _selectedIndex = -1;
    private IReadOnlyList<MediaItem> _items = [];
    private readonly HashSet<long> _selectedIds = [];
    private long? _anchorId; // For shift+click range selection

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

            // Clean up multi-selection - remove IDs that are no longer in the list
            var currentIds = _items.Select(i => i.Id).ToHashSet();
            _selectedIds.IntersectWith(currentIds);

            if (_anchorId.HasValue && !currentIds.Contains(_anchorId.Value))
            {
                _anchorId = null;
            }

            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int Count => _items.Count;

    /// <summary>
    /// Currently selected items (multi-select mode).
    /// </summary>
    public IReadOnlyList<MediaItem> SelectedItems =>
        _items.Where(i => _selectedIds.Contains(i.Id)).ToList();

    /// <summary>
    /// Number of selected items.
    /// </summary>
    public int SelectedCount => _selectedIds.Count;

    /// <summary>
    /// True if more than one item is selected.
    /// </summary>
    public bool IsMultiSelectActive => _selectedIds.Count > 1;

    /// <summary>
    /// Check if a specific item is selected.
    /// </summary>
    public bool IsSelected(MediaItem item) => _selectedIds.Contains(item.Id);

    /// <summary>
    /// Check if a specific item is selected by ID.
    /// </summary>
    public bool IsSelected(long itemId) => _selectedIds.Contains(itemId);

    public event EventHandler<ItemSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler? ItemsChanged;
    public event EventHandler? MultiSelectionChanged;

    /// <summary>
    /// Select an item by reference (single select mode - clears multi-selection).
    /// </summary>
    public void Select(MediaItem? item)
    {
        // Clear multi-selection
        var hadMultiSelect = _selectedIds.Count > 1;
        _selectedIds.Clear();

        if (item is null)
        {
            _selectedIndex = -1;
            _anchorId = null;
            SelectedItem = null;
            if (hadMultiSelect) MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var index = _items.ToList().FindIndex(i => i.Id == item.Id);
        if (index >= 0)
        {
            _selectedIndex = index;
            _selectedIds.Add(item.Id);
            _anchorId = item.Id;
            SelectedItem = _items[index];
        }

        if (hadMultiSelect) MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Select by index (single select mode - clears multi-selection).
    /// </summary>
    public void SelectAt(int index)
    {
        var hadMultiSelect = _selectedIds.Count > 1;
        _selectedIds.Clear();

        if (index < 0 || index >= _items.Count)
        {
            _selectedIndex = -1;
            _anchorId = null;
            SelectedItem = null;
            if (hadMultiSelect) MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _selectedIndex = index;
        var item = _items[index];
        _selectedIds.Add(item.Id);
        _anchorId = item.Id;
        SelectedItem = item;

        if (hadMultiSelect) MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggle selection of an item (Ctrl+Click behavior).
    /// </summary>
    public void ToggleSelection(MediaItem item)
    {
        if (_selectedIds.Contains(item.Id))
        {
            _selectedIds.Remove(item.Id);

            // If we just removed the current primary selection, pick a new one
            if (_selectedItem?.Id == item.Id)
            {
                if (_selectedIds.Count > 0)
                {
                    var firstSelectedId = _selectedIds.First();
                    var newPrimary = _items.FirstOrDefault(i => i.Id == firstSelectedId);
                    if (newPrimary != null)
                    {
                        _selectedIndex = _items.ToList().FindIndex(i => i.Id == firstSelectedId);
                        SelectedItem = newPrimary;
                    }
                }
                else
                {
                    _selectedIndex = -1;
                    SelectedItem = null;
                }
            }
        }
        else
        {
            _selectedIds.Add(item.Id);
            _anchorId = item.Id;

            // Make this the primary selection
            var index = _items.ToList().FindIndex(i => i.Id == item.Id);
            if (index >= 0)
            {
                _selectedIndex = index;
                SelectedItem = _items[index];
            }
        }

        MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Select a range of items (Shift+Click behavior).
    /// Selects from anchor to the specified item.
    /// </summary>
    public void SelectRange(MediaItem endItem)
    {
        if (!_anchorId.HasValue)
        {
            // No anchor, just select the item
            Select(endItem);
            return;
        }

        var anchorIndex = _items.ToList().FindIndex(i => i.Id == _anchorId.Value);
        var endIndex = _items.ToList().FindIndex(i => i.Id == endItem.Id);

        if (anchorIndex < 0 || endIndex < 0)
        {
            Select(endItem);
            return;
        }

        var startIdx = Math.Min(anchorIndex, endIndex);
        var endIdx = Math.Max(anchorIndex, endIndex);

        // Add all items in range to selection
        for (var i = startIdx; i <= endIdx; i++)
        {
            _selectedIds.Add(_items[i].Id);
        }

        // Make the end item the primary selection (but keep anchor)
        _selectedIndex = endIndex;
        SelectedItem = _items[endIndex];

        MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Extend selection while moving (Shift+Arrow behavior).
    /// </summary>
    public void ExtendSelection(int delta)
    {
        if (_items.Count == 0 || _selectedIndex < 0) return;

        var newIndex = Math.Clamp(_selectedIndex + delta, 0, _items.Count - 1);
        var item = _items[newIndex];

        _selectedIds.Add(item.Id);
        _selectedIndex = newIndex;
        SelectedItem = item;

        MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Select all items.
    /// </summary>
    public void SelectAll()
    {
        foreach (var item in _items)
        {
            _selectedIds.Add(item.Id);
        }

        // Keep current primary selection, or select first
        if (_selectedItem is null && _items.Count > 0)
        {
            _selectedIndex = 0;
            _anchorId = _items[0].Id;
            SelectedItem = _items[0];
        }

        MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clear all selection.
    /// </summary>
    public void ClearSelection()
    {
        _selectedIds.Clear();
        _anchorId = null;
        _selectedIndex = -1;
        SelectedItem = null;
        MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Invert selection (Ctrl+Shift+A).
    /// </summary>
    public void InvertSelection()
    {
        var newSelection = new HashSet<long>();
        foreach (var item in _items)
        {
            if (!_selectedIds.Contains(item.Id))
            {
                newSelection.Add(item.Id);
            }
        }

        _selectedIds.Clear();
        foreach (var id in newSelection)
        {
            _selectedIds.Add(id);
        }

        // Update primary selection
        if (_selectedIds.Count > 0)
        {
            var firstId = _selectedIds.First();
            var first = _items.FirstOrDefault(i => i.Id == firstId);
            if (first != null)
            {
                _selectedIndex = _items.ToList().FindIndex(i => i.Id == firstId);
                _anchorId = firstId;
                SelectedItem = first;
            }
        }
        else
        {
            _selectedIndex = -1;
            _anchorId = null;
            SelectedItem = null;
        }

        MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Remove items from selection and list (after delete).
    /// </summary>
    public void RemoveItems(IEnumerable<MediaItem> items)
    {
        var idsToRemove = items.Select(i => i.Id).ToHashSet();
        var list = _items.ToList();

        // Remove from list
        list.RemoveAll(i => idsToRemove.Contains(i.Id));
        _items = list;

        // Remove from selection
        _selectedIds.ExceptWith(idsToRemove);

        // Update primary selection if it was removed
        if (_selectedItem != null && idsToRemove.Contains(_selectedItem.Id))
        {
            if (_selectedIds.Count > 0)
            {
                var firstSelectedId = _selectedIds.First();
                var newPrimary = _items.FirstOrDefault(i => i.Id == firstSelectedId);
                if (newPrimary != null)
                {
                    _selectedIndex = _items.ToList().FindIndex(i => i.Id == firstSelectedId);
                    SelectedItem = newPrimary;
                }
            }
            else if (_items.Count > 0)
            {
                // Select next item after deleted one
                var newIndex = Math.Min(_selectedIndex, _items.Count - 1);
                SelectAt(newIndex);
            }
            else
            {
                _selectedIndex = -1;
                SelectedItem = null;
            }
        }

        ItemsChanged?.Invoke(this, EventArgs.Empty);
        MultiSelectionChanged?.Invoke(this, EventArgs.Empty);
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
