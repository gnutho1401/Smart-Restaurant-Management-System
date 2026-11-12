using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmartRestaurant.WPF.Converters;

/// <summary>
/// Converts boolean to Visibility (with optional inversion via parameter "Invert").
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value switch
        {
            bool boolVal => boolVal,
            string strVal => !string.IsNullOrWhiteSpace(strVal),
            int intVal => intVal != 0,
            null => false,
            _ => true
        };

        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            b = !b;
        }
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
