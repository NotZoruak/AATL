using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 智能等待窗口追踪器：SmartWaitAction 记录等待窗口，供 MATR 层无响应检测排除合法静默。
/// </summary>
public static class SmartWaitTracker
{
    private static DateTime? _waitEndsAt;

    /// <summary>设置等待窗口（等待开始）</summary>
    public static void BeginWait(DateTime endTime)
    {
        _waitEndsAt = endTime;
    }

    /// <summary>清除等待窗口（等待结束/任务停止）</summary>
    public static void Clear()
    {
        _waitEndsAt = null;
    }

    /// <summary>是否正处于智能等待窗口内</summary>
    public static bool IsInWaitWindow()
    {
        return _waitEndsAt != null && DateTime.Now < _waitEndsAt.Value;
    }
}
