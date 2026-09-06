using MaaFramework.Binding;
using MaaFramework.Binding.Custom;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>识别本轮演练是否已经累计三场胜利。</summary>
public sealed class DrillVictoryLimitRecognition : IMaaCustomRecognition
{
    public string Name { get; set; } = nameof(DrillVictoryLimitRecognition);

    public bool Analyze<T>(T context, in AnalyzeArgs args, in AnalyzeResults results) where T : IMaaContext
    {
        return DailyTaskDrillContext.VictoryCount >= 3;
    }
}
