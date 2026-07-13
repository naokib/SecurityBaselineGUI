using System.Globalization;
using System.Windows.Data;

namespace SecurityBaselineGUI.App.Converters;

public sealed class PlaceholderOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 0.6 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
