using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using MFAAvalonia.Extensions.MaaFW;
using System;
using System.Globalization;

namespace MFAAvalonia.Helper.Converters;

public class UniversalEqualityConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
        var paramStr = parameter.ToString();
        var valueStr = value.ToString();

        return valueStr == paramStr;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
        {
            if (targetType.IsEnum && parameter != null)
            {
                if (parameter is string strParam && Enum.IsDefined(targetType, strParam))
                    return Enum.Parse(targetType, strParam);

                if (parameter.GetType() == targetType)
                    return parameter;
            }

            // 处理数字类型
            if (parameter is string strVal)
            {
                try
                {
                    return System.Convert.ChangeType(strVal, targetType);
                }
                catch
                {
                    /* 忽略转换错误 */
                }
            }

            // 处理普通对象
            if (parameter?.GetType() == targetType)
                return parameter;
        }

        return BindingOperations.DoNothing;
    }
}
