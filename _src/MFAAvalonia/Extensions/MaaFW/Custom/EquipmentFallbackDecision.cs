namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>补充刀装兜底流程的坐标决策。</summary>
public static class EquipmentFallbackDecision
{
    /// <summary>根据缺装标记所在行，返回该行的一键装备按钮坐标。</summary>
    public static (int X, int Y) GetOneClickEquipButtonTarget(int matchedY) => (966, matchedY);
}
