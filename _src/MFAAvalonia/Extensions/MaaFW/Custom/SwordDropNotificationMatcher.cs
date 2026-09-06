using System;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public enum SwordDropAnimationKind
{
    Unknown,
    Specialization,
    Kiwame,
    InitialDrop,
}

/// <summary>判断标准刀名是否符合刀剑掉落播报条件。</summary>
public static class SwordDropNotificationMatcher
{
    /// <summary>判断 RGB 颜色的每个通道是否都在指定容差内。</summary>
    public static bool IsColorWithinTolerance(
        byte red,
        byte green,
        byte blue,
        int targetRed,
        int targetGreen,
        int targetBlue,
        int tolerance)
    {
        var safeTolerance = Math.Max(0, tolerance);
        return Math.Abs(red - targetRed) <= safeTolerance
            && Math.Abs(green - targetGreen) <= safeTolerance
            && Math.Abs(blue - targetBlue) <= safeTolerance;
    }

    /// <summary>识别刀剑结果动画标记。</summary>
    public static SwordDropAnimationKind GetAnimationKind(string? text)
    {
        var normalized = string.Concat((text ?? string.Empty).Where(character => !char.IsWhiteSpace(character)));
        if (normalized.Contains('特'))
            return SwordDropAnimationKind.Specialization;

        if (normalized.Contains('极') || normalized.Contains('極'))
            return SwordDropAnimationKind.Kiwame;

        return SwordDropAnimationKind.Unknown;
    }

    /// <summary>生成刀剑掉落通知文本。</summary>
    public static string FormatMessage(string swordType, string swordName) =>
        $"获得 {swordType}「{swordName}」";

    public static bool ShouldNotify(bool enabled, IEnumerable<string>? swords, string? swordName)
    {
        if (!enabled || string.IsNullOrWhiteSpace(swordName) || swords == null)
            return false;

        foreach (var sword in swords)
        {
            if (string.Equals(sword?.Trim(), swordName.Trim(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
