using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System;
using System.Globalization;

namespace SukiUI.Converters;

public class IsCheckedToClassConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is null || value is bool isChecked && isChecked;
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}
