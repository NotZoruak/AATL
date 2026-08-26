using MFAAvalonia.Configuration;
using System;
using System.Globalization;

namespace MFAAvalonia.Services;

/// <summary>处理更新数据任务的触发间隔判断与成功时间记录。</summary>
public static class UpdateDataScheduleService
{
    /// <summary>根据触发间隔判断更新数据任务当前是否需要执行。</summary>
    public static bool ShouldRun(InstanceConfiguration configuration, string interval, DateTime now)
    {
        if (string.Equals(interval, "每次", StringComparison.Ordinal))
            return true;

        var lastSucceeded = GetLastSucceeded(configuration);
        if (lastSucceeded == null)
            return true;

        var currentLocal = NormalizeToLocalTime(now);
        var lastLocal = NormalizeToLocalTime(lastSucceeded.Value);
        return interval switch
        {
            "每天" => currentLocal.Date != lastLocal.Date,
            "每周" => GetIsoWeekKey(currentLocal) != GetIsoWeekKey(lastLocal),
            _ => true,
        };
    }

    /// <summary>读取上次成功完成更新数据任务的时间。</summary>
    public static DateTime? GetLastSucceeded(InstanceConfiguration configuration)
    {
        var rawValue = configuration.GetValue(ConfigurationKeys.UpdateDataLastSucceededAt, string.Empty);
        if (string.IsNullOrWhiteSpace(rawValue))
            return null;

        return DateTime.TryParse(
            rawValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>记录更新数据任务本次成功完成的时间。</summary>
    public static void MarkSucceeded(InstanceConfiguration configuration, DateTime now)
    {
        configuration.SetValue(
            ConfigurationKeys.UpdateDataLastSucceededAt,
            now.ToString("O", CultureInfo.InvariantCulture));
    }

    private static DateTime NormalizeToLocalTime(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value.ToLocalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Local),
        _ => value,
    };

    private static (int Year, int Week) GetIsoWeekKey(DateTime value) =>
        (ISOWeek.GetYear(value), ISOWeek.GetWeekOfYear(value));
}
