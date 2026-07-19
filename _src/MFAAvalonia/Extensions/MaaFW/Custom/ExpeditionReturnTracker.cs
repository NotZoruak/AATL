using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 远征归队时间追踪器：OCR 部队面板后记录最早归队时间，供 SmartWaitAction 计算等待时长。
/// </summary>
public static class ExpeditionReturnTracker
{
    private static DateTime? _earliestReturn;

    /// <summary>设置最早归队时间</summary>
    public static void SetEarliestReturn(DateTime time)
    {
        _earliestReturn = time;
    }

    /// <summary>重置追踪器（任务启动或全部空闲时清零）</summary>
    public static void Reset()
    {
        _earliestReturn = null;
    }

    /// <summary>获取剩余秒数，若无远征或已过期返回 0</summary>
    public static int GetRemainingSeconds()
    {
        if (_earliestReturn == null)
            return 0;

        var remaining = (_earliestReturn.Value - DateTime.Now).TotalSeconds;
        return remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
    }

    /// <summary>是否有远征正在执行</summary>
    public static bool HasActiveExpedition()
    {
        return _earliestReturn != null && DateTime.Now < _earliestReturn.Value;
    }
}
