using System.Globalization;
using System.Windows.Data;

namespace ThwipKit.Wpf.Converters;

public sealed class NullToFalseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
