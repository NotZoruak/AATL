using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class TimedWaitAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(TimedWaitAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var hour = 0;
            var minute = 0;
            if (!string.IsNullOrWhiteSpace(args.ActionParam))
            {
                var json = ActionParamHelper.Parse(args.ActionParam);
                hour = (int?)json["hour"] ?? 0;
                minute = (int?)json["minute"] ?? 0;
            }

            var now = DateTime.Now;
            var target = now.Date.AddHours(hour).AddMinutes(minute);

            // 如果目标时间已过，则等待到明天的该时间
            if (target <= now)
                target = target.AddDays(1);

            var waitTime = target - now;
            LoggerHelper.Info($"定时等待开始：target={target:yyyy-MM-dd HH:mm}, remainingMinutes={waitTime.TotalMinutes:F1}");

            // 分段等待，每30秒检查一次，避免长时间阻塞
            while (DateTime.Now < target)
            {
                ActionParamHelper.ThrowIfStopping(context);
                var remaining = target - DateTime.Now;
                var sleepMs = Math.Min((int)remaining.TotalMilliseconds, 30000);
                if (sleepMs <= 0) break;
                ActionParamHelper.SleepWithStopCheck(context, sleepMs);
            }

            LoggerHelper.Info($"定时等待结束：已到达目标时间 {target:yyyy-MM-dd HH:mm}，继续执行");
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("定时等待检测到手动停止，已中止等待");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"定时等待异常：{e.Message}", e);
            return false;
        }
    }
}
