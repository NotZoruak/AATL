namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 长期远征计划——刷花状态追踪器（静态，跨节点共享）。
/// </summary>
public static class FlowerStateTracker
{
    /// <summary>当前刷花目标部队编号（1~5）</summary>
    public static int TargetTeam { get; set; }

    /// <summary>当前刷花循环轮数</summary>
    public static int LoopCount { get; set; }

    /// <summary>最近一次疲劳检测的最低值</summary>
    public static int CurrentFatigueLowest { get; set; }

    /// <summary>最大循环轮数上限</summary>
    public const int MaxLoops = 30;

    public static bool IsMaxLoopsExceeded() => LoopCount >= MaxLoops;

    public static void Reset()
    {
        TargetTeam = 0;
        LoopCount = 0;
        CurrentFatigueLowest = 0;
    }

    /// <summary>进入新部队刷花时调用</summary>
    public static void BeginTeam(int team)
    {
        TargetTeam = team;
        LoopCount = 0;
        CurrentFatigueLowest = 0;
    }

    /// <summary>每轮刷花循环开始时调用，返回是否超限</summary>
    public static bool NextLoop()
    {
        LoopCount++;
        return IsMaxLoopsExceeded();
    }
}
