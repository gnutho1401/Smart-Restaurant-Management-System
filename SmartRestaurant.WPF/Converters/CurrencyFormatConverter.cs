using System;
using System.Globalization;
using System.Windows.Data;

namespace SmartRestaurant.WPF.Converters;

/// <summary>
/// Formats numerical amounts into Vietnamese currency string (e.g. 450,000 đ).
/// </summary>
public class CurrencyFormatConverter : IValueConverter
{
    private static readonly CultureInfo ViVnCulture = new("vi-VN");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return $"{d.ToString("#,##0", ViVnCulture)} đ";

        if (value is int i)
            return $"{i.ToString("#,##0", ViVnCulture)} đ";

        if (value is double db)
            return $"{db.ToString("#,##0", ViVnCulture)} đ";

        return "0 đ";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
