using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 最后一步撤退判定：非激进策略在仅余一次行动、且王点不相邻时返回 true。
/// </summary>
public sealed class EdoLastActionRetreatRecognition : IMaaCustomRecognition
{
    private static int _retreatPending;

    public string Name { get; set; } = nameof(EdoLastActionRetreatRecognition);

    public bool Analyze<T>(T context, in AnalyzeArgs args, in AnalyzeResults results) where T : IMaaContext
    {
        var param = ActionParamHelper.Parse(args.RecognitionParam);
        var strategy = (string?)param["strategy"] ?? "balanced";
        if (string.Equals(strategy, "aggressive", StringComparison.OrdinalIgnoreCase))
            return false;

        using var image = context.GetImage();
        if (image == null || EdoActionSelectAction.ReadActionCount(context, image) != 1)
            return false;

        var currentPoint = EdoActionSelectAction.FindCurrentPoint(context, image);
        if (currentPoint == null || currentPoint == "P16")
            return false;

        EdoActionSelectAction.ClearState();
        Interlocked.Exchange(ref _retreatPending, 1);
        return true;
    }

    public static bool ConsumeRetreatPending() => Interlocked.Exchange(ref _retreatPending, 0) == 1;

    public static void ResetRetreatPending() => Interlocked.Exchange(ref _retreatPending, 0);
}

/// <summary>在回到活动页后区分主动撤退与正常完成一圈。</summary>
public sealed class EdoRetreatCompletionRecognition : IMaaCustomRecognition
{
    public string Name { get; set; } = nameof(EdoRetreatCompletionRecognition);

    public bool Analyze<T>(T context, in AnalyzeArgs args, in AnalyzeResults results) where T : IMaaContext
    {
        return EdoLastActionRetreatRecognition.ConsumeRetreatPending();
    }
}
