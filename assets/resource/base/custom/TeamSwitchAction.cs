using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class TeamSwitchAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(TeamSwitchAction);

    private static readonly int[][] TeamButtons = new[]
    {
        new[] { 850, 479, 1, 1 },
        new[] { 1007, 475, 1, 1 },
        new[] { 1167, 473, 1, 1 },
        new[] { 931, 608, 1, 1 },
        new[] { 1077, 606, 1, 1 },
    };

    private static readonly int[] ConfirmButton = { 931, 353, 1, 1 };
    private static readonly int[] RoundRoi = { 114, 39, 28, 25 };

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var json = ActionParamHelper.Parse(args.ActionParam);
            var teamConfig = (string?)json["team_config"] ?? "111111111";

            if (teamConfig.Length != 9)
            {
                LoggerHelper.Error($"[TeamSwitch] 队伍配置长度必须为9，当前长度: {teamConfig.Length}");
                return false;
            }

            foreach (char c in teamConfig)
            {
                if (c < '1' || c > '5')
                {
                    LoggerHelper.Error($"[TeamSwitch] 队伍配置包含非法字符: {c}");
                    return false;
                }
            }

            LoggerHelper.Info($"[TeamSwitch] 队伍配置: {teamConfig}");

            // OCR 识别当前轮次
            int remaining = ReadRemaining(context);
            if (remaining < 1 || remaining > 9)
            {
                LoggerHelper.Error($"[TeamSwitch] 剩余轮次不在换队范围内: {remaining}");
                return false;
            }

            int nextRound = 11 - remaining;
            int teamIndex = teamConfig[9 - remaining] - '1';
            LoggerHelper.Info($"[TeamSwitch] 剩余{remaining}轮(即将第{nextRound}轮) → 部队{teamIndex + 1}");

            // 双击队伍按钮
            var btn = TeamButtons[teamIndex];
            int cx = btn[0] + btn[2] / 2;
            int cy = btn[1] + btn[3] / 2;
            context.Click(cx, cy);
            ActionParamHelper.SleepWithStopCheck(context, 500);
            context.Click(cx, cy);
            ActionParamHelper.SleepWithStopCheck(context, 500);

            // 点确认
            int confirmX = ConfirmButton[0] + ConfirmButton[2] / 2;
            int confirmY = ConfirmButton[1] + ConfirmButton[3] / 2;
            context.Click(confirmX, confirmY);
            ActionParamHelper.SleepWithStopCheck(context, 500);

            LoggerHelper.Info($"[TeamSwitch] 换队完成: 第{nextRound}轮 → 部队{teamIndex + 1}");
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[TeamSwitch] 手动停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[TeamSwitch] Error: {e.Message}");
            return false;
        }
    }

    private int ReadRemaining<T>(T context) where T : IMaaContext
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);

            using var image = context.GetImage();
            if (image == null)
            {
                ActionParamHelper.SleepWithStopCheck(context, 400);
                continue;
            }

            var text = context.GetText(RoundRoi[0], RoundRoi[1], RoundRoi[2], RoundRoi[3], image);
            LoggerHelper.Info($"[TeamSwitch] 轮次 OCR 结果: '{text}' (第{attempt + 1}次)");

            // OCR 可能把数字识别成字母
            text = text.Replace("O", "0").Replace("o", "0")
                       .Replace("l", "1").Replace("I", "1")
                       .Replace("Z", "2").Replace("S", "5");

            if (int.TryParse(text, out int digit) && digit >= 1 && digit <= 10)
            {
                return digit;
            }

            ActionParamHelper.SleepWithStopCheck(context, 400);
        }

        return -1;
    }
}
