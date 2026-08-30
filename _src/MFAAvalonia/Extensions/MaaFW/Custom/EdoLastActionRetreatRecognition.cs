using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using Newtonsoft.Json.Linq;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 最后一步撤退判定：非激进策略在仅余一次行动、且王点不相邻时返回 true。
/// </summary>
public sealed class EdoLastActionRetreatRecognition : IMaaCustomRecognition
{
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
        return true;
    }
}
