using System.Globalization;
using System.Windows.Data;

namespace FileTracker.App.Converters;

/// <summary>
/// Inverts a boolean value. true → false, false → true.
/// Used for the Outgoing radio button which is checked when IsIncoming is false.
/// </summary>
public class BoolInvertConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }
}
