namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 疲劳首位识别失败时的流程判断。
/// </summary>
public static class FatigueCheckDecision
{
    /// <summary>首位没有疲劳值时继续出阵，避免误入刷花流程。</summary>
    public static bool ShouldContinueWhenFirstValueUnreadable(int? firstValue)
    {
        return !firstValue.HasValue;
    }
}
