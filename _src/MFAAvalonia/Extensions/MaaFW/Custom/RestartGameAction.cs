using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class RestartGameAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(RestartGameAction);

    private string? _adbPath;
    private string? _adbSerial;
    private string? _mumuPath;
    private int _mumuIndex;

    private void EnsureAdbInfo()
    {
        if (_adbPath != null) return;

        var processor = MaaProcessorManager.Instance.Current;
        if (processor != null)
        {
            _adbPath = processor.Config.AdbDevice.AdbPath;
            _adbSerial = processor.Config.AdbDevice.AdbSerial;

            // 从 Config JSON 提取 MuMu 安装路径和实例编号
            var configStr = processor.Config.AdbDevice.Config;
            if (!string.IsNullOrWhiteSpace(configStr))
            {
                try
                {
                    using var doc = JsonDocument.Parse(configStr);
                    if (doc.RootElement.TryGetProperty("extras", out var extras) &&
                        extras.TryGetProperty("mumu", out var mumu))
                    {
                        if (mumu.TryGetProperty("path", out var path))
                            _mumuPath = path.GetString();
                        if (mumu.TryGetProperty("index", out var index))
                            _mumuIndex = index.GetInt32();
                    }
                }
                catch { }
            }
        }
        _adbPath ??= "adb";
        _adbSerial ??= "";
        _mumuPath ??= "";
    }

    private static string GetPackageName()
    {
        var globalOpts = MaaProcessor.Interface?.GlobalSelectOptions;
        var targetOpt = globalOpts?.FirstOrDefault(o => o.Name == "目标应用");
        if (targetOpt?.Data != null && targetOpt.Data.TryGetValue("package_name", out var pkg) && !string.IsNullOrWhiteSpace(pkg))
            return pkg;
        return "com.youzu.djlw";
    }

    private void RestartEmulator()
    {
        if (string.IsNullOrWhiteSpace(_mumuPath) || !Directory.Exists(_mumuPath))
        {
            LoggerHelper.Info("[RestartGameAction] 未找到 MuMu 安装路径，跳过模拟器重启");
            return;
        }

        // 查找 MuMuPlayer.exe
        var playerExe = Path.Combine(_mumuPath, "MuMuPlayer.exe");
        if (!File.Exists(playerExe))
        {
            LoggerHelper.Info("[RestartGameAction] 未找到 MuMuPlayer.exe，跳过模拟器重启");
            return;
        }

        // 杀模拟器进程
        LoggerHelper.Info("[RestartGameAction] 正在关闭模拟器...");
        var killPsi = new ProcessStartInfo("taskkill", "/F /IM MuMuPlayer.exe /T")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        Process.Start(killPsi)?.WaitForExit(5000);
        Thread.Sleep(3000);

        // 重启模拟器
        LoggerHelper.Info("[RestartGameAction] 正在启动模拟器...");
        var startArgs = _mumuIndex > 0 ? $"-v {_mumuIndex}" : "";
        var startPsi = new ProcessStartInfo(playerExe, startArgs)
        {
            UseShellExecute = true,
        };
        Process.Start(startPsi);

        // 等待 ADB 重新连接
        LoggerHelper.Info("[RestartGameAction] 等待模拟器启动...");
        Thread.Sleep(15000);
        for (int i = 0; i < 30; i++)
        {
            var checkPsi = new ProcessStartInfo(_adbPath!, $"shell echo ready")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            if (!string.IsNullOrWhiteSpace(_adbSerial))
                checkPsi.Arguments = $"-s {_adbSerial} shell echo ready";
            var proc = Process.Start(checkPsi);
            proc?.WaitForExit(5000);
            if (proc?.ExitCode == 0)
            {
                LoggerHelper.Info("[RestartGameAction] 模拟器已就绪");
                return;
            }
            Thread.Sleep(2000);
        }
        LoggerHelper.Info("[RestartGameAction] 模拟器启动超时，继续后续流程");
    }

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            EnsureAdbInfo();

            var package = GetPackageName();

            // 0. 重启模拟器（模拟器重启会连带杀死游戏进程）
            RestartEmulator();

            // 1. 重新启动游戏
            LoggerHelper.Info($"[RestartGameAction] 重新启动游戏: {package}");
            var startPsi = new ProcessStartInfo(_adbPath!, $"shell monkey -p {package} -c android.intent.category.LAUNCHER 1")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            if (!string.IsNullOrWhiteSpace(_adbSerial))
                startPsi.Arguments = $"-s {_adbSerial} shell monkey -p {package} -c android.intent.category.LAUNCHER 1";
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
