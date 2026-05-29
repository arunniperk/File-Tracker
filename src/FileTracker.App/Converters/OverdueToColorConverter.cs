using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FileTracker.App.Converters;

/// <summary>
/// Converts a boolean value to a Brush color.
/// Returns Red when true, default (Black) when false.
/// </summary>
public class OverdueToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isOverdue = value is true;
        return isOverdue ? Brushes.Red : Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
