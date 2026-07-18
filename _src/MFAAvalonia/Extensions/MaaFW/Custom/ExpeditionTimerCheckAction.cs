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
                var msg = "远征检查倒计时已到，即将回本丸检查远征";
                LoggerHelper.Info(msg);
                try { MaaProcessorManager.Instance.Current?.AddLog(msg); } catch { }
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
            LoggerHelper.Error($"[ExpeditionTimerCheckAction] 错误: {e.Message}");
            return false;
        }
    }
}
