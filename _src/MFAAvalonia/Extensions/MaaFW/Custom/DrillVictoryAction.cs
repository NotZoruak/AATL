using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using Newtonsoft.Json.Linq;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>保存当前日课演练已确认的胜利数量。</summary>
public static class DailyTaskDrillContext
{
    public static int VictoryCount { get; private set; }

    public static void Reset() => VictoryCount = 0;

    public static int AddVictory() => ++VictoryCount;
}

/// <summary>开始处理五个演练位置前清空胜利计数。</summary>
public class DrillResetVictoryAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(DrillResetVictoryAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        DailyTaskDrillContext.Reset();
        LoggerHelper.Info("[日课 演练] 开始处理演练对手");
        return true;
    }
}

/// <summary>记录已胜利的位置。</summary>
public class DrillVictoryAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(DrillVictoryAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var json = ActionParamHelper.Parse(args.ActionParam);
        var position = (int?)json["position"] ?? 0;
        var count = DailyTaskDrillContext.AddVictory();
        LoggerHelper.Info($"日课 演练 位置{position} 胜利（累计 {count} 胜）");
        LoggerHelper.Info("[日课] 完成一圈");
        return true;
    }
}
