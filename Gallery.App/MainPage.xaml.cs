using Gallery.App.Services;
using Gallery.App.ViewModels;
using Gallery.Domain.Models;

namespace Gallery.App;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private readonly SelectionService _selection;
    private readonly PrefetchService _prefetch;

    public MainPage(MainViewModel viewModel, SelectionService selection, PrefetchService prefetch)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _selection = selection;
        _prefetch = prefetch; // Hold reference to keep it alive
        BindingContext = viewModel;
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
}
