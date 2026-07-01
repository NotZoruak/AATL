using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SukiUI.Converters;

public class IfConditionConverter : IValueConverter
{
    public object? True { get; set; }

    public object? False { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !(value is bool flag) || !flag ? this.False : this.True;
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

