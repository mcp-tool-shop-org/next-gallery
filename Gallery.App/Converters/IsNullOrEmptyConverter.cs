using System.Globalization;

namespace Gallery.App.Converters;

/// <summary>
/// Returns true if the value is null or empty string.
/// </summary>
public class IsNullOrEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => true,
            string s => string.IsNullOrEmpty(s),
            _ => false
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
