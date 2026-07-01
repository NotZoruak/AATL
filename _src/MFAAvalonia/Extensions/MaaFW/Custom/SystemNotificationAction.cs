using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class SystemNotificationAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(SystemNotificationAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var title = "MFAAvalonia";
        var message = "任务通知";
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            if (!string.IsNullOrWhiteSpace(args.ActionParam))
            {
                var json = ActionParamHelper.Parse(args.ActionParam);
                title = (string?)json["title"] ?? title;
                message = (string?)json["message"] ?? message;
            }

            LoggerHelper.Info($"发送系统通知：title={title}, messageLength={message.Length}");
            ToastNotification.Show(title, message);
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("发送系统通知前检测到手动停止，已取消发送");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"发送系统通知失败：title={title}, 原因：{e.Message}", e);
            return false;
        }
    }
}
