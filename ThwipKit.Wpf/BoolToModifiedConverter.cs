using System.Globalization;
using System.Windows.Data;

namespace ThwipKit.Wpf;

public sealed class BoolToModifiedConverter : IValueConverter
{
    public static readonly BoolToModifiedConverter Instance = new();

    public object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "*" : string.Empty;

    public object? ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
