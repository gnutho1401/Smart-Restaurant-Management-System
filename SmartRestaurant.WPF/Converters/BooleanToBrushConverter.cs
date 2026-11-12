using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartRestaurant.WPF.Converters;

public class BooleanToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#27AE60"));
    private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#E74C3C"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is true;
        return b ? GreenBrush : RedBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
