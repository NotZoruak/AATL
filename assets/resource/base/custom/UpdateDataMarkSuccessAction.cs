using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>记录更新数据任务最后一次完整成功时间。</summary>
public sealed class UpdateDataMarkSuccessAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(UpdateDataMarkSuccessAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            var interval = ActionParamHelper.Parse(args.ActionParam)["interval"]?.Value<string>() ?? "每次";
            var configuration = ConfigurationManager.CurrentInstance;
            UpdateDataScheduleService.MarkSucceeded(configuration, DateTime.Now);
            LoggerHelper.Info($"[更新数据] 任务完成，已记录触发间隔：{interval}");
            return true;
        }
        catch (MaaStopException)
        {
            return false;
        }
        catch (Exception exception)
        {
            LoggerHelper.Error($"[更新数据] 记录成功时间失败：{exception.Message}");
            return false;
        }
    }
}
