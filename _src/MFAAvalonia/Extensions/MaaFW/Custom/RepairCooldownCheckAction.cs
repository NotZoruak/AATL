using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 检查修刀冷却状态。冷却期间返回失败，跳过当前修刀检查。
/// </summary>
public class RepairCooldownCheckAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(RepairCooldownCheckAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            var active = RepairCooldownState.IsActive(DateTime.UtcNow);
            if (active)
                LoggerHelper.Info("[后勤修刀] 无可修刀，冷却期间跳过修刀检查");
            return !active;
        }
        catch (MaaStopException)
        {
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[后勤修刀] 冷却状态检查失败：{e.Message}");
            return false;
        }
    }
}
