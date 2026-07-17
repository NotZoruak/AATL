using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;
using System.Diagnostics;
using System.Threading;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class RestartGameAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(RestartGameAction);

    private const string PackageName = "com.youzu.djlw";

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            // 强杀游戏进程
            LoggerHelper.Info($"[RestartGameAction] 强杀游戏进程: {PackageName}");
            var killPsi = new ProcessStartInfo("adb", $"shell am force-stop {PackageName}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            Process.Start(killPsi)?.WaitForExit(3000);

            // 等一下再启动
            Thread.Sleep(2000);

            // 重新启动游戏
            LoggerHelper.Info($"[RestartGameAction] 重新启动游戏: {PackageName}");
            var startPsi = new ProcessStartInfo("adb", $"shell monkey -p {PackageName} -c android.intent.category.LAUNCHER 1")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            Process.Start(startPsi)?.WaitForExit(3000);

            LoggerHelper.Info("[RestartGameAction] 游戏重启完成");
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[RestartGameAction] 检测到手动停止，已取消执行");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[RestartGameAction] 错误: {e.Message}");
            return false;
        }
    }
}
