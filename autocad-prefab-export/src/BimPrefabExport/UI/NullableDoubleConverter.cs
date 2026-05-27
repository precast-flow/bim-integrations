using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BimPrefabExport.UI;

/// <summary><see cref="double?"/> alanları DataGrid metin kutusunda düzenlemek için.</summary>
public sealed class NullableDoubleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return "";
        if (value is double d)
            return d.ToString(culture);
        return value.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return null;
        var t = s.Trim().Replace(',', '.');
        if (double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;
        if (double.TryParse(s.Trim(), NumberStyles.Any, culture, out v))
            return v;
        return DependencyProperty.UnsetValue;
    }
}
