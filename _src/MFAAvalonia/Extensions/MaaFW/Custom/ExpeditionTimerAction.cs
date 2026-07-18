using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 远征后台计时器动作：记录倒计时起点，供 ExpeditionTimerRecognition 检查。
/// </summary>
public class ExpeditionTimerAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(ExpeditionTimerAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            var json = ActionParamHelper.Parse(args.ActionParam);
            var mode = (string?)json["mode"] ?? "start";

            if (mode == "reset")
            {
                ExpeditionTimerRecognition.ResetTimer();
                return true;
            }

            int intervalSeconds = (int?)json["interval"] ?? 600;
            ExpeditionTimerRecognition.StartTimer(intervalSeconds);
            var display = intervalSeconds >= 60
                ? $"{intervalSeconds / 60} 分钟"
                : $"{intervalSeconds} 秒";
            var msg = $"远征检查倒计时开始：{display}";
            LoggerHelper.Info(msg);
            try { MaaProcessorManager.Instance.Current?.AddLog(msg); } catch { }
            return true;
        }
        catch (MaaStopException)
        {
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[ExpeditionTimerAction] 错误: {e.Message}");
            return false;
        }
    }
}
