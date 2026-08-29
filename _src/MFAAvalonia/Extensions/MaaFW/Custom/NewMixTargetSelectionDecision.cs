using System.Collections.Generic;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>新习合目标扫描结果。</summary>
public enum NewMixTargetSelectionOutcome
{
    NoSword,
    Locked,
    Normal,
    Completed,
    Unreadable
}

/// <summary>一个习合目标位的识别结果。</summary>
public readonly record struct NewMixTargetSlot(bool HasSword, bool IsLocked, int? Level);

/// <summary>新习合目标扫描的纯决策逻辑。</summary>
public static class NewMixTargetSelectionDecision
{
    /// <summary>根据六个目标位的状态决定下一步处理方式。</summary>
    public static NewMixTargetSelectionPlan Decide(IReadOnlyList<NewMixTargetSlot> slots)
    {
        if (slots.Count == 0 || !slots[0].HasSword)
            return new(NewMixTargetSelectionOutcome.NoSword, 0);

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            if (!slot.HasSword)
                continue;

            if (slot.IsLocked)
                return new(NewMixTargetSelectionOutcome.Locked, index + 1);

            if (slot.Level is null)
                continue;

            if (slot.Level < 7)
                return new(NewMixTargetSelectionOutcome.Normal, index + 1);
        }

        return new(NewMixTargetSelectionOutcome.Completed, 0);
    }
}

/// <summary>新习合目标扫描的决策结果与目标位置。</summary>
public readonly record struct NewMixTargetSelectionPlan(NewMixTargetSelectionOutcome Outcome, int Position);

/// <summary>在自定义 action 与后续自定义识别之间保存本轮扫描结果。</summary>
public static class NewMixTargetSelectionState
{
    /// <summary>最近一次目标扫描结果。</summary>
    public static NewMixTargetSelectionPlan Current { get; set; } = new(NewMixTargetSelectionOutcome.Unreadable, 0);
}
