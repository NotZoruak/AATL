using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 远征计时器检查动作：返回 true=计时器过期（走 next），返回 false=未过期（走 on_error）。
/// </summary>
public class ExpeditionTimerCheckAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(ExpeditionTimerCheckAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            if (ExpeditionTimerRecognition.IsExpired())
            {
                // 智能调度关闭时计时器从未启动，不输出误导性日志
                if (ExpeditionTimeTracker.IsSmartSchedulingEnabled())
                {
                    var msg = "[远征计时] 倒计时结束";
                    LoggerHelper.Info(msg);
                    try { MaaProcessorManager.Instance.Current?.AddLog(msg); } catch { }
                }
                return false;
            }
            else
            {
                return true;
            }
        }
        catch (MaaStopException)
        {
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[远征计时] 错误: {e.Message}");
            return false;
        }
    }
}
