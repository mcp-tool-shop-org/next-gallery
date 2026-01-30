using Gallery.Domain.Models;

namespace Gallery.App.TemplateSelectors;

/// <summary>
/// Selects the appropriate template for timeline rows (header vs tile row).
/// </summary>
public class TimelineRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? TileRowTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            GroupHeaderRow => HeaderTemplate,
            TileRow => TileRowTemplate,
            _ => TileRowTemplate
        };
    }
}
