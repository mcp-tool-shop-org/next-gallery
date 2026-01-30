using Gallery.App.Services;
using Gallery.Domain.Models;

namespace Gallery.App.Views;

public partial class QuickPreviewOverlay : ContentView
{
    private readonly SelectionService _selection;

    public QuickPreviewOverlay(SelectionService selection)
    {
        InitializeComponent();
        _selection = selection;
        _selection.SelectionChanged += OnSelectionChanged;

        // Initial load
        UpdatePreview(_selection.SelectedItem);
    }

    private void OnSelectionChanged(object? sender, ItemSelectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdatePreview(e.Current);
        });
    }

    private void UpdatePreview(MediaItem? item)
    {
        if (item is null)
        {
            PreviewImage.Source = null;
            FileInfoLabel.Text = string.Empty;
            LoadingIndicator.IsVisible = false;
            return;
        }

        // Show loading initially
        LoadingIndicator.IsVisible = true;

        // Try large thumb first, fall back to small, then original
        var imagePath = item.ThumbLargePath ?? item.ThumbSmallPath ?? item.Path;

        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            PreviewImage.Source = ImageSource.FromFile(imagePath);
            LoadingIndicator.IsVisible = false;
        }
        else
        {
            PreviewImage.Source = null;
        }

        // Update file info
        var fileName = Path.GetFileName(item.Path);
        var dimensions = item.Width.HasValue && item.Height.HasValue
            ? $" • {item.Width}×{item.Height}"
            : "";
        FileInfoLabel.Text = $"{fileName}{dimensions}";
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            _selection.SelectionChanged -= OnSelectionChanged;
        }
    }
}
