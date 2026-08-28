using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using Newtonsoft.Json.Linq;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>读取新习合目标扫描结果，用于将流水线分流到对应处理路径。</summary>
public sealed class NewMixTargetSelectionRecognition : IMaaCustomRecognition
{
    public string Name { get; set; } = nameof(NewMixTargetSelectionRecognition);

    public bool Analyze<T>(T context, in AnalyzeArgs args, in AnalyzeResults results) where T : IMaaContext
    {
        var param = ActionParamHelper.Parse(args.RecognitionParam);
        var outcomeText = (string?)param["outcome"];
        return Enum.TryParse<NewMixTargetSelectionOutcome>(outcomeText, out var expected)
            && NewMixTargetSelectionState.Current.Outcome == expected;
    }
}
