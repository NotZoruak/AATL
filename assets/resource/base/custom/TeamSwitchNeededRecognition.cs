using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>判断当前行动选择界面是否需要切换到目标部队。</summary>
public sealed class TeamSwitchNeededRecognition : IMaaCustomRecognition
{
    private static readonly int[] RoundRoi = { 114, 39, 28, 25 };

    public string Name { get; set; } = nameof(TeamSwitchNeededRecognition);

    public bool Analyze<T>(T context, in AnalyzeArgs args, in AnalyzeResults results) where T : IMaaContext
    {
        var param = ActionParamHelper.Parse(args.RecognitionParam);
        var teamConfig = (string?)param["team_config"] ?? "111111111";
        var initialTeam = int.TryParse((string?)param["initial_team"], out var parsedTeam)
            ? parsedTeam
            : 1;

        using var image = context.GetImage();
        if (image == null)
        {
            return false;
        }

        var text = context.GetText(RoundRoi[0], RoundRoi[1], RoundRoi[2], RoundRoi[3], image)
            .Replace("O", "0")
            .Replace("o", "0")
            .Replace("l", "1")
            .Replace("I", "1")
            .Replace("Z", "2")
            .Replace("S", "5");
        if (!int.TryParse(text, out var remaining) || remaining is < 1 or > 9)
        {
            return false;
        }

        return TeamSwitchState.ShouldSwitch(teamConfig, remaining, initialTeam);
    }
}
