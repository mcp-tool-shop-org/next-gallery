using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gallery.Application.Interfaces;
using Gallery.App.Services;
using Gallery.Domain.Enums;
using Gallery.Domain.Models;
using Gallery.Infrastructure.Services;

namespace Gallery.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILibraryStore _libraryStore;
    private readonly IMediaItemStore _itemStore;
    private readonly IItemIndexService _indexService;
    private readonly ThumbWorker _thumbWorker;
    private readonly SelectionService _selection;

    // Grid layout info for keyboard navigation
    private int _columnsPerRow = 6;
    private int _itemsPerPage = 24;

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
    private bool _isQuickPreviewOpen;

    public MainViewModel(
        ILibraryStore libraryStore,
        IMediaItemStore itemStore,
        IItemIndexService indexService,
        ThumbWorker thumbWorker,
        SelectionService selection)
    {
        _libraryStore = libraryStore;
        _itemStore = itemStore;
        _indexService = indexService;
        _thumbWorker = thumbWorker;
        _selection = selection;

        _thumbWorker.ThumbGenerated += OnThumbGenerated;
        _selection.SelectionChanged += OnSelectionChanged;
    }

    public async Task InitializeAsync()
    {
        await LoadFoldersAsync();
        await LoadItemsAsync();
        _thumbWorker.Start();
    }

    /// <summary>
    /// Update grid layout info for keyboard navigation.
    /// </summary>
    public void SetGridLayout(int columns, int visibleRows)
    {
        _columnsPerRow = columns;
        _itemsPerPage = columns * visibleRows;
    }

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
            await LoadItemsAsync();
            ScanStatus = $"Scan complete. {ItemCount} items indexed.";
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
            await LoadItemsAsync();
            ScanStatus = $"Scan complete. {ItemCount} items indexed.";
        }
        finally
        {
            IsScanning = false;
        }
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

        await Launcher.Default.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(SelectedItem.Path)
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadItemsAsync();
    }

    #endregion

    #region Data Loading

    private async Task LoadFoldersAsync()
    {
        var folders = await _libraryStore.GetAllAsync();
        Folders = new ObservableCollection<LibraryFolder>(folders);
    }

    private async Task LoadItemsAsync()
    {
        var items = await _itemStore.GetAllAsync(limit: 5000);
        Items = new ObservableCollection<MediaItem>(items);
        ItemCount = Items.Count;

        // Update selection service
        _selection.Items = items;
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
