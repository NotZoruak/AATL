using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using Newtonsoft.Json.Linq;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class CountdownAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(CountdownAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var seconds = 60;
            if (!string.IsNullOrWhiteSpace(args.ActionParam))
            {
                var json = ActionParamHelper.Parse(args.ActionParam);
                seconds = (int?)json["seconds"] ?? 60;
            }

            LoggerHelper.Info($"[CountdownAction] 倒计时 {seconds} 秒");
            for (int i = seconds; i > 0; i--)
            {
                ActionParamHelper.SleepWithStopCheck(context, 1000);
            }

            LoggerHelper.Info("[CountdownAction] 倒计时结束");
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[CountdownAction] 检测到手动停止，已中止倒计时");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[CountdownAction] Error: {e.Message}");
            return false;
        }
    }
}
