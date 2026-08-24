using System.Globalization;
using System.Windows.Data;

namespace ThwipKit.Wpf.Converters;

public sealed class ByteSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not uint bytes)
        {
            return string.Empty;
        }

        return bytes >= 1024 * 1024
            ? $"{bytes / (1024d * 1024d):N2} MB"
            : bytes >= 1024
                ? $"{bytes / 1024d:N2} KB"
                : $"{bytes:N0} B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
