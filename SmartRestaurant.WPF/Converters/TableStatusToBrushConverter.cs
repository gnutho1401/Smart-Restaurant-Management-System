using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.WPF.Converters;

/// <summary>
/// Converts DFA TableStatus enum to high-contrast modern UI solid color brushes.
/// Available: Green (#27AE60) | Occupied: Red (#E74C3C) | Reserved: Blue (#2980B9) | Cleaning: Amber (#F39C12)
/// </summary>
public class TableStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#27AE60"));
    private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#E74C3C"));
    private static readonly SolidColorBrush BlueBrush = new((Color)ColorConverter.ConvertFromString("#2980B9"));
    private static readonly SolidColorBrush AmberBrush = new((Color)ColorConverter.ConvertFromString("#F39C12"));
    private static readonly SolidColorBrush GrayBrush = new((Color)ColorConverter.ConvertFromString("#95A5A6"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TableStatus status)
        {
            return status switch
            {
                TableStatus.Available => GreenBrush,
                TableStatus.Occupied => RedBrush,
                TableStatus.Reserved => BlueBrush,
                TableStatus.Cleaning => AmberBrush,
                _ => GrayBrush
            };
        }
        return GrayBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
