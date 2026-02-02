using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gallery.Application.Interfaces;
using Gallery.App.Services;
using Gallery.Domain.Enums;
using Gallery.Domain.Models;
using Gallery.Infrastructure.Services;
using Microsoft.Maui.ApplicationModel;

namespace Gallery.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILibraryStore _libraryStore;
    private readonly IMediaItemStore _itemStore;
    private readonly IItemIndexService _indexService;
    private readonly IMediaActionService _actionService;
    private readonly ThumbWorker _thumbWorker;
    private readonly SelectionService _selection;
    private readonly QueryService _query;

    // Grid layout info for keyboard navigation
    private int _columnsPerRow = 6;
    private int _itemsPerPage = 24;

    // Debounce for search
    private CancellationTokenSource? _searchDebounce;
    private const int SearchDebounceMs = 200;

    [ObservableProperty]
    private ObservableCollection<LibraryFolder> _folders = [];

    [ObservableProperty]
    private ObservableCollection<MediaItem> _items = [];

    [ObservableProperty]
    private MediaItem? _selectedItem;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = string.Empty;

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isQuickPreviewOpen;

    // Search and filter state
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private MediaTypeFilter _mediaTypeFilter = MediaTypeFilter.All;

    [ObservableProperty]
    private SortField _sortField = SortField.ModifiedAt;

    [ObservableProperty]
    private SortDir _sortDirection = SortDir.Desc;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    // Grouping state
    [ObservableProperty]
    private GroupBy _groupBy = GroupBy.None;

    [ObservableProperty]
    private ObservableCollection<MediaGroup> _groups = [];

    /// <summary>
    /// Virtualized timeline rows for grouped mode (headers + tile rows).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TimelineRow> _timelineRows = [];

    /// <summary>
    /// True when the grid should display grouped items.
    /// </summary>
    public bool IsGrouped => GroupBy != GroupBy.None;

    // Multi-select state
    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _isMultiSelectActive;

    [ObservableProperty]
    private bool _isActionInProgress;

    [ObservableProperty]
    private string _actionStatus = string.Empty;

    [ObservableProperty]
    private int _actionProgress;

    [ObservableProperty]
    private int _actionTotal;

    public MainViewModel(
        ILibraryStore libraryStore,
        IMediaItemStore itemStore,
        IItemIndexService indexService,
        IMediaActionService actionService,
        ThumbWorker thumbWorker,
        SelectionService selection,
        QueryService query)
    {
        _libraryStore = libraryStore;
        _itemStore = itemStore;
        _indexService = indexService;
        _actionService = actionService;
        _thumbWorker = thumbWorker;
        _selection = selection;
        _query = query;

        _thumbWorker.ThumbGenerated += OnThumbGenerated;
        _selection.SelectionChanged += OnSelectionChanged;
        _selection.MultiSelectionChanged += OnMultiSelectionChanged;
        _query.QueryChanged += OnQueryChanged;
    }

    public async Task InitializeAsync()
    {
        await LoadFoldersAsync();
        await ExecuteQueryAsync();
        _thumbWorker.Start();
    }

    /// <summary>
    /// Update grid layout info for keyboard navigation.
    /// Also rebuilds timeline rows if column count changed in grouped mode.
    /// </summary>
    public void SetGridLayout(int columns, int visibleRows)
    {
        var columnsChanged = _columnsPerRow != columns;
        _columnsPerRow = columns;
        _itemsPerPage = columns * visibleRows;

        // Rebuild timeline rows if columns changed in grouped mode
        if (columnsChanged && IsGrouped && Groups.Count > 0)
        {
            var rows = TimelineRowBuilder.BuildRows(Groups, columns);
            TimelineRows = new ObservableCollection<TimelineRow>(rows);
        }
    }

    #region Search and Filters

    partial void OnSearchTextChanged(string value)
    {
        DebounceSearch();
    }

    partial void OnFavoritesOnlyChanged(bool value)
    {
        ApplyFiltersImmediate();
    }

    partial void OnMediaTypeFilterChanged(MediaTypeFilter value)
    {
        ApplyFiltersImmediate();
    }

    partial void OnSortFieldChanged(SortField value)
    {
        ApplyFiltersImmediate();
    }

    partial void OnSortDirectionChanged(SortDir value)
    {
        ApplyFiltersImmediate();
    }

    partial void OnGroupByChanged(GroupBy value)
    {
        OnPropertyChanged(nameof(IsGrouped));
        ApplyFiltersImmediate();
    }

    private void DebounceSearch()
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        var ct = _searchDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SearchDebounceMs, ct);
                if (!ct.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(() => ApplyFiltersImmediate());
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when typing rapidly
            }
        }, ct);
    }

    private void ApplyFiltersImmediate()
    {
        _query.Set(new LibraryQuery(
            Text: SearchText,
            MediaType: MediaTypeFilter,
            FavoritesOnly: FavoritesOnly,
            SortBy: SortField,
            SortDir: SortDirection,
            GroupBy: GroupBy
        ));
    }

    [RelayCommand]
    private void ToggleFavoritesFilter()
    {
        FavoritesOnly = !FavoritesOnly;
    }

    [RelayCommand]
    private void SetMediaTypeAll() => MediaTypeFilter = MediaTypeFilter.All;

    [RelayCommand]
    private void SetMediaTypeImages() => MediaTypeFilter = MediaTypeFilter.Images;

    [RelayCommand]
    private void SetMediaTypeVideos() => MediaTypeFilter = MediaTypeFilter.Videos;

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        FavoritesOnly = false;
        MediaTypeFilter = MediaTypeFilter.All;
        SortField = SortField.ModifiedAt;
        SortDirection = SortDir.Desc;
        GroupBy = GroupBy.None;
    }

    [RelayCommand]
    private void SetGroupByNone() => GroupBy = GroupBy.None;

    [RelayCommand]
    private void SetGroupByDay() => GroupBy = GroupBy.Day;

    [RelayCommand]
    private void SetGroupByMonth() => GroupBy = GroupBy.Month;

    [RelayCommand]
    private void CycleGroupBy()
    {
        GroupBy = GroupBy switch
        {
            GroupBy.None => GroupBy.Day,
            GroupBy.Day => GroupBy.Month,
            GroupBy.Month => GroupBy.None,
            _ => GroupBy.None
        };
    }

    private async void OnQueryChanged(LibraryQuery query)
    {
        await ExecuteQueryAsync();
    }

    private async Task ExecuteQueryAsync()
    {
        // Capture current selection to restore if possible
        var previousSelectedId = SelectedItem?.Id;

        IReadOnlyList<MediaItem> allItems;

        if (_query.Current.IsGrouped)
        {
            // Grouped mode
            var result = await _itemStore.QueryGroupedAsync(_query.Current, limit: 5000);
            Groups = new ObservableCollection<MediaGroup>(result.Groups);
            TotalCount = result.TotalCount;

            // Build virtualized timeline rows
            var rows = TimelineRowBuilder.BuildRows(result.Groups, _columnsPerRow);
            TimelineRows = new ObservableCollection<TimelineRow>(rows);

            // Flatten for selection service
            allItems = result.Groups.SelectMany(g => g.Items).ToList();
            Items = new ObservableCollection<MediaItem>(allItems);
            ItemCount = allItems.Count;
        }
        else
        {
            // Flat mode
            var result = await _itemStore.QueryAsync(_query.Current, limit: 5000);
            Items = new ObservableCollection<MediaItem>(result.Items);
            Groups = new ObservableCollection<MediaGroup>();
            TimelineRows = new ObservableCollection<TimelineRow>();
            ItemCount = result.Items.Count;
            TotalCount = result.TotalCount;
            allItems = result.Items;
        }

        // Update selection service
        _selection.Items = allItems;

        // Try to restore selection
        if (previousSelectedId.HasValue)
        {
            var stillExists = allItems.FirstOrDefault(i => i.Id == previousSelectedId);
            if (stillExists is not null)
            {
                _selection.Select(stillExists);
            }
            else if (allItems.Count > 0)
            {
                _selection.SelectFirst();
            }
        }
        else if (allItems.Count > 0 && SelectedItem is null)
        {
            _selection.SelectFirst();
        }

        // Update result summary
        UpdateResultSummary();
    }

    private void UpdateResultSummary()
    {
        var groupInfo = _query.Current.IsGrouped
            ? $" • {Groups.Count} {(_query.Current.GroupBy == GroupBy.Day ? "days" : "months")}"
            : "";

        if (_query.Current.HasFilters)
        {
            ResultSummary = $"{ItemCount:N0} of {TotalCount:N0} items{groupInfo}";
        }
        else
        {
            ResultSummary = $"{TotalCount:N0} items{groupInfo}";
        }
    }

    #endregion

    #region Keyboard Navigation

    [RelayCommand]
    private void MoveLeft() => _selection.Move(-1);

    [RelayCommand]
    private void MoveRight() => _selection.Move(1);

    [RelayCommand]
    private void MoveUp() => _selection.MoveRow(-1, _columnsPerRow);

    [RelayCommand]
    private void MoveDown() => _selection.MoveRow(1, _columnsPerRow);

    [RelayCommand]
    private void MoveHome() => _selection.SelectRowStart(_columnsPerRow);

    [RelayCommand]
    private void MoveEnd() => _selection.SelectRowEnd(_columnsPerRow);

    [RelayCommand]
    private void MoveFirst() => _selection.SelectFirst();

    [RelayCommand]
    private void MoveLast() => _selection.SelectLast();

    [RelayCommand]
    private void PageUp() => _selection.PageMove(-1, _itemsPerPage);

    [RelayCommand]
    private void PageDown() => _selection.PageMove(1, _itemsPerPage);

    [RelayCommand]
    private void ToggleQuickPreview()
    {
        if (SelectedItem is not null)
        {
            IsQuickPreviewOpen = !IsQuickPreviewOpen;
        }
    }

    [RelayCommand]
    private void CloseQuickPreview()
    {
        IsQuickPreviewOpen = false;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        if (IsQuickPreviewOpen)
        {
            IsQuickPreviewOpen = false;
        }
        else
        {
            _selection.Select(null);
        }
    }

    [RelayCommand]
    private void SelectItem(MediaItem? item)
    {
        _selection.Select(item);
    }

    #endregion

    #region Library Management

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (result.IsSuccessful && !string.IsNullOrEmpty(result.Folder?.Path))
        {
            var folder = await _libraryStore.AddAsync(result.Folder.Path);
            Folders.Add(folder);
            await ScanFolderAsync(folder);
        }
    }

    [RelayCommand]
    private async Task RemoveFolderAsync(LibraryFolder folder)
    {
        await _libraryStore.RemoveAsync(folder.Id);
        Folders.Remove(folder);
    }

    [RelayCommand]
    private async Task ScanAllAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        ScanStatus = "Starting scan...";

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                ScanStatus = $"Scanning: {Path.GetFileName(p.CurrentFile)} ({p.FilesScanned}/{p.FilesTotal})";
            });

            await _indexService.ScanAllAsync(progress);
            await ExecuteQueryAsync();
            ScanStatus = $"Scan complete. {TotalCount} items indexed.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task ScanFolderAsync(LibraryFolder folder)
    {
        if (IsScanning) return;

        IsScanning = true;
        ScanStatus = $"Scanning {folder.Path}...";

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                ScanStatus = $"Scanning: {Path.GetFileName(p.CurrentFile)} ({p.FilesScanned}/{p.FilesTotal})";
            });

            await _indexService.ScanFolderAsync(folder, progress);
            await ExecuteQueryAsync();
            ScanStatus = $"Scan complete. {TotalCount} items indexed.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteQueryAsync();
    }

    #endregion

    #region Item Actions

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (SelectedItem is null) return;

        var newValue = !SelectedItem.IsFavorite;
        await _itemStore.SetFavoriteAsync(SelectedItem.Id, newValue);
        var updated = SelectedItem with { IsFavorite = newValue };
        _selection.UpdateItem(updated);
        UpdateItemInCollection(updated);

        // If favorites filter is on and we unfavorited, item may disappear
        if (FavoritesOnly && !newValue)
        {
            await ExecuteQueryAsync();
        }
    }

    [RelayCommand]
    private async Task SetRatingAsync(int rating)
    {
        if (SelectedItem is null) return;

        await _itemStore.SetRatingAsync(SelectedItem.Id, rating);
        var updated = SelectedItem with { Rating = rating };
        _selection.UpdateItem(updated);
    }

    [RelayCommand]
    private async Task OpenInExplorerAsync()
    {
        if (SelectedItem is null) return;

        await _actionService.OpenInExplorerAsync(SelectedItem);
    }

    [RelayCommand]
    private async Task OpenWithDefaultAppAsync()
    {
        if (SelectedItem is null) return;

        await _actionService.OpenWithDefaultAppAsync(SelectedItem);
    }

    #endregion

    #region Multi-Select Actions

    /// <summary>
    /// Toggle selection of an item (Ctrl+Click).
    /// </summary>
    [RelayCommand]
    private void ToggleItemSelection(MediaItem? item)
    {
        if (item is null) return;
        _selection.ToggleSelection(item);
    }

    /// <summary>
    /// Select range from anchor to item (Shift+Click).
    /// </summary>
    [RelayCommand]
    private void SelectRange(MediaItem? item)
    {
        if (item is null) return;
        _selection.SelectRange(item);
    }

    /// <summary>
    /// Select all items (Ctrl+A).
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        _selection.SelectAll();
    }

    /// <summary>
    /// Invert selection (Ctrl+Shift+A).
    /// </summary>
    [RelayCommand]
    private void InvertSelection()
    {
        _selection.InvertSelection();
    }

    /// <summary>
    /// Extend selection while navigating (Shift+Arrow).
    /// </summary>
    [RelayCommand]
    private void ExtendSelectionLeft() => _selection.ExtendSelection(-1);

    [RelayCommand]
    private void ExtendSelectionRight() => _selection.ExtendSelection(1);

    [RelayCommand]
    private void ExtendSelectionUp() => _selection.ExtendSelection(-_columnsPerRow);

    [RelayCommand]
    private void ExtendSelectionDown() => _selection.ExtendSelection(_columnsPerRow);

    #endregion

    #region Batch Actions (Agency!)

    /// <summary>
    /// Delete selected items (move to trash by default).
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var items = _selection.SelectedItems;
        if (items.Count == 0) return;

        // TODO: Show confirmation dialog
        await ExecuteActionAsync(
            $"Deleting {items.Count} item(s)...",
            async (progress, ct) => await _actionService.DeleteAsync(items, permanent: false, progress, ct),
            items);
    }

    /// <summary>
    /// Permanently delete selected items (no trash).
    /// </summary>
    [RelayCommand]
    private async Task DeletePermanentlyAsync()
    {
        var items = _selection.SelectedItems;
        if (items.Count == 0) return;

        // TODO: Show confirmation dialog with warning
        await ExecuteActionAsync(
            $"Permanently deleting {items.Count} item(s)...",
            async (progress, ct) => await _actionService.DeleteAsync(items, permanent: true, progress, ct),
            items);
    }

    /// <summary>
    /// Move selected items to a folder.
    /// </summary>
    [RelayCommand]
    private async Task MoveSelectedAsync()
    {
        var items = _selection.SelectedItems;
        if (items.Count == 0) return;

        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful || string.IsNullOrEmpty(result.Folder?.Path)) return;

        await ExecuteActionAsync(
            $"Moving {items.Count} item(s)...",
            async (progress, ct) => await _actionService.MoveAsync(items, result.Folder.Path, progress, ct),
            items);
    }

    /// <summary>
    /// Copy selected items to a folder.
    /// </summary>
    [RelayCommand]
    private async Task CopySelectedAsync()
    {
        var items = _selection.SelectedItems;
        if (items.Count == 0) return;

        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (!result.IsSuccessful || string.IsNullOrEmpty(result.Folder?.Path)) return;

        await ExecuteActionAsync(
            $"Copying {items.Count} item(s)...",
            async (progress, ct) => await _actionService.CopyAsync(items, result.Folder.Path, progress, ct),
            null); // Don't remove from list on copy
    }

    /// <summary>
    /// Set favorite on all selected items.
    /// </summary>
    [RelayCommand]
    private async Task SetSelectedFavoriteAsync(bool favorite)
    {
        var items = _selection.SelectedItems;
        if (items.Count == 0) return;

        IsActionInProgress = true;
        ActionStatus = $"Setting favorite on {items.Count} item(s)...";

        try
        {
            foreach (var item in items)
            {
                await _itemStore.SetFavoriteAsync(item.Id, favorite);
                var updated = item with { IsFavorite = favorite };
                _selection.UpdateItem(updated);
                UpdateItemInCollection(updated);
            }

            ActionStatus = $"Updated {items.Count} item(s)";

            // Refresh if favorites filter is active
            if (FavoritesOnly)
            {
                await ExecuteQueryAsync();
            }
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    /// <summary>
    /// Set rating on all selected items.
    /// </summary>
    [RelayCommand]
    private async Task SetSelectedRatingAsync(int rating)
    {
        var items = _selection.SelectedItems;
        if (items.Count == 0) return;

        IsActionInProgress = true;
        ActionStatus = $"Setting rating on {items.Count} item(s)...";

        try
        {
            foreach (var item in items)
            {
                await _itemStore.SetRatingAsync(item.Id, rating);
                var updated = item with { Rating = rating };
                _selection.UpdateItem(updated);
                UpdateItemInCollection(updated);
            }

            ActionStatus = $"Updated {items.Count} item(s)";
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    /// <summary>
    /// Quick export to Downloads folder.
    /// </summary>
    [RelayCommand]
    private async Task QuickExportAsync()
    {
        var items = _selection.SelectedItems;
        if (items.Count == 0 && SelectedItem != null)
        {
            items = [SelectedItem];
        }
        if (items.Count == 0) return;

        var downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        downloads = Path.Combine(downloads, "Downloads");

        await ExecuteActionAsync(
            $"Exporting {items.Count} item(s) to Downloads...",
            async (progress, ct) => await _actionService.CopyAsync(items, downloads, progress, ct),
            null);
    }

    /// <summary>
    /// Execute a batch action with progress tracking.
    /// </summary>
    private async Task ExecuteActionAsync(
        string statusMessage,
        Func<IProgress<ActionProgress>, CancellationToken, Task<ActionResult>> action,
        IReadOnlyList<MediaItem>? itemsToRemove)
    {
        if (IsActionInProgress) return;

        IsActionInProgress = true;
        ActionStatus = statusMessage;
        ActionProgress = 0;
        ActionTotal = 100;

        try
        {
            var progress = new Progress<ActionProgress>(p =>
            {
                ActionProgress = p.Completed;
                ActionTotal = p.Total;
                ActionStatus = $"{statusMessage} ({p.Completed}/{p.Total})";
            });

            var result = await action(progress, CancellationToken.None);

            if (result.Success)
            {
                ActionStatus = $"Completed: {result.SuccessCount} item(s)";

                if (itemsToRemove != null && itemsToRemove.Count > 0)
                {
                    _selection.RemoveItems(itemsToRemove);
                    await ExecuteQueryAsync();
                }
            }
            else
            {
                ActionStatus = $"Completed with errors: {result.SuccessCount} succeeded, {result.FailureCount} failed";
                // TODO: Show error details dialog
            }
        }
        catch (Exception ex)
        {
            ActionStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsActionInProgress = false;
        }
    }

    #endregion

    #region Data Loading

    private async Task LoadFoldersAsync()
    {
        var folders = await _libraryStore.GetAllAsync();
        Folders = new ObservableCollection<LibraryFolder>(folders);
    }

    #endregion

    #region Event Handlers

    private void OnSelectionChanged(object? sender, ItemSelectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SelectedItem = e.Current;
        });
    }

    private void OnMultiSelectionChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SelectedCount = _selection.SelectedCount;
            IsMultiSelectActive = _selection.IsMultiSelectActive;
        });
    }

    private void OnThumbGenerated(object? sender, ThumbGeneratedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var item = Items.FirstOrDefault(i => i.Id == e.ItemId);
            if (item is null) return;

            var updated = e.Size == ThumbSize.Small
                ? item with { ThumbSmallPath = e.ThumbPath }
                : item with { ThumbLargePath = e.ThumbPath };

            UpdateItemInCollection(updated);
            _selection.UpdateItem(updated);
        });
    }

    private void UpdateItemInCollection(MediaItem updated)
    {
        var index = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Id == updated.Id)
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            Items[index] = updated;
        }
    }

    #endregion

    /// <summary>
    /// Handle grid selection from UI (syncs back to SelectionService).
    /// </summary>
    public void OnGridSelectionChanged(MediaItem? item)
    {
        _selection.Select(item);
    }
}
