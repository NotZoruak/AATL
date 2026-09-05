using System;

namespace MFAAvalonia.Services;

/// <summary>格式化核心资源图表数据点的悬停提示。</summary>
public static class WarehouseChartTooltipFormatter
{
    public static string Format(DateTime recordedAt, int value, int? change)
    {
        var changeText = change.HasValue
            ? $"变动：{change.Value:+#,#;-#,#;0}"
            : "变动：—";
        return $"{recordedAt:yyyy-MM-dd HH:mm:ss}\n当前：{value:N0}\n{changeText}";
    }
}
