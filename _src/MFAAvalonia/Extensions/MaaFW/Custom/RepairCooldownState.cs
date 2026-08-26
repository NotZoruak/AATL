using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 记录无可修刀后的修刀冷却时间。
/// </summary>
public static class RepairCooldownState
{
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(30);

    private static DateTime _cooldownUntilUtc;

    public static void Start(DateTime nowUtc)
    {
        _cooldownUntilUtc = nowUtc + Cooldown;
    }

    public static bool IsActive(DateTime nowUtc)
    {
        return nowUtc < _cooldownUntilUtc;
    }
}
