using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SmartRestaurant.Core.Enums;

namespace SmartRestaurant.WPF.Converters;

/// <summary>
/// Converts TableStatus to soft, modern pastel background brush for badges.
/// </summary>
public class TableStatusToBadgeBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBg = new((Color)ColorConverter.ConvertFromString("#DCFCE7"));
    private static readonly SolidColorBrush RedBg = new((Color)ColorConverter.ConvertFromString("#FEE2E2"));
    private static readonly SolidColorBrush BlueBg = new((Color)ColorConverter.ConvertFromString("#DBEAFE"));
    private static readonly SolidColorBrush AmberBg = new((Color)ColorConverter.ConvertFromString("#FEF3C7"));
    private static readonly SolidColorBrush GrayBg = new((Color)ColorConverter.ConvertFromString("#F1F5F9"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TableStatus status)
        {
            return status switch
            {
                TableStatus.Available => GreenBg,
                TableStatus.Occupied => RedBg,
                TableStatus.Reserved => BlueBg,
                TableStatus.Cleaning => AmberBg,
                _ => GrayBg
            };
        }
        return GrayBg;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>
/// Converts TableStatus to strong, readable text foreground brush for badges.
/// </summary>
public class TableStatusToBadgeForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenFg = new((Color)ColorConverter.ConvertFromString("#15803D"));
    private static readonly SolidColorBrush RedFg = new((Color)ColorConverter.ConvertFromString("#B91C1C"));
    private static readonly SolidColorBrush BlueFg = new((Color)ColorConverter.ConvertFromString("#1D4ED8"));
    private static readonly SolidColorBrush AmberFg = new((Color)ColorConverter.ConvertFromString("#B45309"));
    private static readonly SolidColorBrush GrayFg = new((Color)ColorConverter.ConvertFromString("#475569"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TableStatus status)
        {
            return status switch
            {
                TableStatus.Available => GreenFg,
                TableStatus.Occupied => RedFg,
                TableStatus.Reserved => BlueFg,
                TableStatus.Cleaning => AmberFg,
                _ => GrayFg
            };
        }
        return GrayFg;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>
/// Converts TableStatus to Vietnamese localized status label.
/// </summary>
public class TableStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TableStatus status)
        {
            return status switch
            {
                TableStatus.Available => "● Sẵn Sàng",
                TableStatus.Occupied => "● Đang Dùng",
                TableStatus.Reserved => "● Đã Đặt Trước",
                TableStatus.Cleaning => "● Chờ Dọn Dẹp",
                _ => "● Không Xác Định"
            };
        }
        return "● Trống";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
