using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>为更新数据流程提供可中断的短暂等待。</summary>
public sealed class UpdateDataWaitAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(UpdateDataWaitAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            var milliseconds = 1000;
            if (!string.IsNullOrWhiteSpace(args.ActionParam))
                milliseconds = Math.Max(0, (int?)ActionParamHelper.Parse(args.ActionParam)["milliseconds"] ?? milliseconds);
            ActionParamHelper.SleepWithStopCheck(context, milliseconds);
            return true;
        }
        catch (MaaStopException)
        {
            return false;
        }
    }
}
