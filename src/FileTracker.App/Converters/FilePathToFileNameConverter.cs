using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace FileTracker.App.Converters;

/// <summary>
/// Extracts just the filename from a full file path.
/// Used in the pending attachments list to show only the filename, not the full path.
/// </summary>
public class FilePathToFileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            return Path.GetFileName(path);
        }
        return value ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
