using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SukiUI.Converters;

public class BoolToPasswordCharConverter : MarkupExtension, IMultiValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = SafeGetValue<bool>(values, 0);
        var passwordChar = SafeGetValue<char>(values, 1, '*');
        return visible ? '\0' : passwordChar;
    }

    /// <summary>
    /// 安全获取绑定值（处理 UnsetValue 和类型转换）
    /// </summary>
    private T SafeGetValue<T>(IList<object?> values, int index, T defaultValue = default)
    {
        if (index >= values.Count) return defaultValue;

        var value = values[index];

        // 处理 Avalonia 的 UnsetValue
        if (value is UnsetValueType || value == null) return defaultValue;

        try
        {
            return (T)System.Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}
