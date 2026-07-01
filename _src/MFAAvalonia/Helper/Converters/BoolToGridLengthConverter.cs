using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace MFAAvalonia.Helper.Converters;

public class BoolToGridLengthConverter : IValueConverter
{
    // value为HasColor的值（bool）
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool hasColor && hasColor)
        {
            // HasColor为true时，列宽为*（占一半）
            return GridLength.Auto;
        }
        // HasColor为false时，列宽为0（隐藏）
        return new GridLength(0);
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}
