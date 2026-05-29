using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileTracker.App.Converters;

/// <summary>
/// Converts an integer count to Visibility.
/// Returns Collapsed when value is 0 or null, Visible otherwise.
/// Parameter "Inverse" flips the logic: Visible when 0, Collapsed otherwise.
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value as int? ?? 0;
        var invert = parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);

        var isNonZero = count > 0;
        var visible = invert ? !isNonZero : isNonZero;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
