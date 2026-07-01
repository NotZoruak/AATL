using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using MFAAvalonia.Helper.ValueType;
using MFAAvalonia.ViewModels.Windows;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MFAAvalonia.Helper.Converters;

public class AnnouncementTitleConverter : MarkupExtension, IMultiValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // 安全解包参数（处理 UnsetValue 和 null）
        var type = SafeGetValue<AnnouncementType>(values, 0);
        var titleA = SafeGetValue<string>(values, 1);
        var titleB = SafeGetValue<string>(values, 2);
        switch (type)
        {
            case AnnouncementType.Release:
                return titleA;
            case AnnouncementType.Changelog:
                return titleB;
            default:
                return titleB;
        }
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
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
