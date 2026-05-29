using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FileTracker.App.Converters;

/// <summary>
/// Maps true → Collapsed, false → Visible (inverse of BoolToVisibilityConverter).
/// </summary>
public class BoolInvertVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible ? false : true;
    }
}
