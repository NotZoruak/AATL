using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class RestartGameAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(RestartGameAction);

    private static string GetPackageName()
    {
        var globalOpts = MaaProcessor.Interface?.GlobalSelectOptions;
        var targetOpt = globalOpts?.FirstOrDefault(o => o.Name == "目标应用");
        if (targetOpt?.Data != null && targetOpt.Data.TryGetValue("package_name", out var pkg) && !string.IsNullOrWhiteSpace(pkg))
            return pkg;
        return "com.youzu.djlw";
    }

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            // 强杀游戏进程
            LoggerHelper.Info($"[RestartGameAction] 强杀游戏进程: {GetPackageName()}");
            var killPsi = new ProcessStartInfo("adb", $"shell am force-stop {GetPackageName()}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            Process.Start(killPsi)?.WaitForExit(3000);

            // 等一下再启动
            Thread.Sleep(2000);

            // 重新启动游戏
            LoggerHelper.Info($"[RestartGameAction] 重新启动游戏: {GetPackageName()}");
            var startPsi = new ProcessStartInfo("adb", $"shell monkey -p {GetPackageName()} -c android.intent.category.LAUNCHER 1")
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
