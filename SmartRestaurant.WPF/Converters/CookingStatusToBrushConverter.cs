using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.WPF.Converters;

/// <summary>
/// Converts KDS CookingStatus to visual indicator brushes.
/// Pending: Orange | Cooking: Yellow | Ready: Green | Served: Gray
/// </summary>
public class CookingStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush PendingBrush = new((Color)ColorConverter.ConvertFromString("#E67E22"));
    private static readonly SolidColorBrush CookingBrush = new((Color)ColorConverter.ConvertFromString("#F1C40F"));
    private static readonly SolidColorBrush ReadyBrush = new((Color)ColorConverter.ConvertFromString("#2ECC71"));
    private static readonly SolidColorBrush ServedBrush = new((Color)ColorConverter.ConvertFromString("#7F8C8D"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CookingStatus status)
        {
            return status switch
            {
                CookingStatus.Pending => PendingBrush,
                CookingStatus.Cooking => CookingBrush,
                CookingStatus.Ready => ReadyBrush,
                CookingStatus.Served => ServedBrush,
                _ => ServedBrush
            };
        }
        return ServedBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
