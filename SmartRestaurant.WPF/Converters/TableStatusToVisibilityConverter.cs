using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.WPF.Converters;

/// <summary>
/// Converts a TableStatus to Visibility based on the target status string parameter (e.g. "Available", "Occupied", "Cleaning").
/// Supports comma-separated multi-state parameters (e.g. "Available,Occupied").
/// </summary>
public class TableStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TableStatus status && parameter is string targetStatusStr)
        {
            var targets = targetStatusStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var t in targets)
            {
                if (Enum.TryParse<TableStatus>(t, true, out var targetStatus) && status == targetStatus)
                {
                    return Visibility.Visible;
                }
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

