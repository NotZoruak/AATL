using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SukiUI.Converters;

public class ComboBoxItemConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0] = ComboBoxItem对象 (通过RelativeSource获取)
        // values[1] = DisplayMemberBinding对象
        if (value == null || value is not ComboBoxItem item || item.Parent is not ComboBox comboBox)
            return AvaloniaProperty.UnsetValue;
        var result = item.DataContext;
        if (comboBox.DisplayMemberBinding == null || comboBox.DisplayMemberBinding is not Binding binding)
            return result;

        var path = binding.Path; // 从Binding对象提取Path

        if (string.IsNullOrEmpty(path))
            return AvaloniaProperty.UnsetValue;

        return result?.GetType().GetProperty(path)?.GetValue(result);
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return
            AvaloniaProperty.UnsetValue
            ;
    }
}
