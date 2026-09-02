namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>根据威胁度阈值判断是否应进入演练。</summary>
public static class DrillDangerDecision
{
    /// <summary>威胁度达到阈值时避战，否则进入演练。</summary>
    public static bool ShouldEnterTraining(int danger, int threshold) => danger < threshold;
}
