using System.Globalization;
using System.Windows.Data;

namespace FileTracker.App.Converters;

/// <summary>
/// Maps true → "Active", false → "Inactive".
/// </summary>
public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Active" : "Inactive";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
