using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MFAAvalonia.Helper.Converters;

public class MaxWidthConverter : MarkupExtension, IMultiValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        var minWidth = 0;
        if (values.Count < 2 || !double.TryParse(values[0]?.ToString(), out double parentWidth) || !double.TryParse(values[1]?.ToString(), out double firstWidth))
            return minWidth;

        var availableWidth = parentWidth - firstWidth;
        return Math.Max(availableWidth, 0);
    }
}
