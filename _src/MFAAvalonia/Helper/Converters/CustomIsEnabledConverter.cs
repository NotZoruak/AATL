using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MFAAvalonia.Helper.Converters;

public class CustomIsEnabledConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is [bool isChecked, bool idle])
        {
            bool isCheckedValue = isChecked;
            return (isCheckedValue && idle) || !isCheckedValue;
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return [];
    }
}
