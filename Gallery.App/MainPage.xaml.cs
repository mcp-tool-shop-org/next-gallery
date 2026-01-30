using Gallery.App.Services;
using Gallery.App.ViewModels;
using Gallery.Domain.Models;

namespace Gallery.App;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly SelectionService _selection;
    private readonly PrefetchService _prefetch;

    // Resize debounce
    private CancellationTokenSource? _resizeDebounce;
    private const int ResizeDebounceMs = 150;

    // Grid layout constants
    private const int TileSize = 140;
    private const int TileSpacing = 4;
    private const int LeftPanelWidth = 220;
    private const int RightPanelWidth = 320;

    public MainPage(MainViewModel viewModel, SelectionService selection, PrefetchService prefetch)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _selection = selection;
        _prefetch = prefetch; // Hold reference to keep it alive
        BindingContext = viewModel;

        // Subscribe to preview close to stop video
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Subscribe to size changes for column recalculation
        SizeChanged += OnPageSizeChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsQuickPreviewOpen))
        {
            if (!_viewModel.IsQuickPreviewOpen)
            {
                // Stop video when preview closes
                QuickPreviewVideo?.Stop();
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();

        // Set up keyboard handling for Windows
        SetupKeyboardHandling();
    }

    private void SetupKeyboardHandling()
    {
#if WINDOWS
        var window = this.Window;
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window winUIWindow)
        {
            winUIWindow.Content.KeyDown += OnKeyDown;
        }
#endif
    }

#if WINDOWS
    private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var handled = true;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left:
                _viewModel.MoveLeftCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.Right:
                _viewModel.MoveRightCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.Up:
                _viewModel.MoveUpCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.Down:
                _viewModel.MoveDownCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.Home:
                if (IsControlPressed())
                    _viewModel.MoveFirstCommand.Execute(null);
                else
                    _viewModel.MoveHomeCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.End:
                if (IsControlPressed())
                    _viewModel.MoveLastCommand.Execute(null);
                else
                    _viewModel.MoveEndCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.PageUp:
                _viewModel.PageUpCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.PageDown:
                _viewModel.PageDownCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.Space:
                _viewModel.ToggleQuickPreviewCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.Escape:
                _viewModel.ClearSelectionCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.Enter:
                _viewModel.ToggleQuickPreviewCommand.Execute(null);
                break;
            case Windows.System.VirtualKey.F:
                _viewModel.ToggleFavoriteCommand.Execute(null);
                break;
            default:
                handled = false;
                break;
        }

        e.Handled = handled;
    }

    private static bool IsControlPressed()
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }
#endif

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var item = e.CurrentSelection.FirstOrDefault() as MediaItem;
        _viewModel.OnGridSelectionChanged(item);
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        // Debounce resize to avoid thrashing
        _resizeDebounce?.Cancel();
        _resizeDebounce = new CancellationTokenSource();
        var ct = _resizeDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ResizeDebounceMs, ct);
                if (!ct.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(RecalculateGridLayout);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when resizing rapidly
            }
        }, ct);
    }

    private void RecalculateGridLayout()
    {
        // Calculate available width for grid (total width - panels)
        var availableWidth = Width - LeftPanelWidth - RightPanelWidth - 16; // 16 for padding

        if (availableWidth <= 0) return;

        // Calculate columns that fit
        var columns = Math.Max(1, (int)(availableWidth / (TileSize + TileSpacing)));

        // Calculate visible rows (approximate)
        var availableHeight = Height - 100; // Header + status bar
        var visibleRows = Math.Max(1, (int)(availableHeight / (TileSize + TileSpacing)));

        _viewModel.SetGridLayout(columns, visibleRows);
    }
}
