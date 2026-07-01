using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;
using System.Diagnostics;
using System.IO;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class KillProcessAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(KillProcessAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            var processName = string.Empty;
            var killSelfProcess = false;
            if (!string.IsNullOrWhiteSpace(args.ActionParam))
            {
                var json = ActionParamHelper.Parse(args.ActionParam);
                processName = (string?)json["process_name"] ?? string.Empty;
                killSelfProcess = (bool?)json["kill_self_process"] ?? string.IsNullOrWhiteSpace(processName);
            }

            if (killSelfProcess)
            {
                using var currentProcess = Process.GetCurrentProcess();
                LoggerHelper.Info($"[KillProcessAction] 结束自身进程: {currentProcess.ProcessName} (PID: {currentProcess.Id})");
                currentProcess.Kill();
                return true;
            }

            if (string.IsNullOrWhiteSpace(processName))
            {
                LoggerHelper.Warning("[KillProcessAction] 未指定进程名");
                return false;
            }

            var normalizedProcessName = Path.GetFileNameWithoutExtension(processName.Trim());
            LoggerHelper.Info($"[KillProcessAction] 结束进程: {normalizedProcessName}");
            var processes = Process.GetProcessesByName(normalizedProcessName);
            foreach (var proc in processes)
            {
                try
                {
                    ActionParamHelper.ThrowIfStopping(context);
                    proc.Kill();
                    proc.WaitForExit(5000);
                    LoggerHelper.Info($"[KillProcessAction] 已结束: {proc.ProcessName} (PID: {proc.Id})");
                }
                catch (Exception ex)
                {
                    LoggerHelper.Warning($"[KillProcessAction] 结束进程失败: {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }

            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[KillProcessAction] 检测到手动停止，已取消执行");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[KillProcessAction] Error: {e.Message}");
            return false;
        }
    }
}
