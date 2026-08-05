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
    private string? _mumuCliExe;         // mumu-cli.exe 完整路径（MuMu 12+ 通过 CLI 控制）
    private string? _mumuLegacyExe;      // 旧版 MuMuPlayer.exe 完整路径
    private string? _mumuProcessName;    // 旧版要杀的进程名
    private bool _isMuMu12;              // 是否为 MuMu 12+（支持 CLI）

    private void EnsureAdbInfo()
    {
        if (_adbPath != null) return;

        var processor = MaaProcessorManager.Instance.Current;
        if (processor != null)
        {
            _adbPath = processor.Config.AdbDevice.AdbPath;
            _adbSerial = processor.Config.AdbDevice.AdbSerial;

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

        DetectMuMuEnvironment();
    }

    /// <summary>
    /// 探测 MuMu 环境：MuMu 12+ 优先用 CLI（mumu-cli.exe），旧版回退到 MuMuPlayer.exe
    /// </summary>
    private void DetectMuMuEnvironment()
    {
        if (string.IsNullOrWhiteSpace(_mumuPath) || !Directory.Exists(_mumuPath))
            return;

        // MuMu 12+：使用 mumu-cli.exe 控制实例重启
        var cliExe = Path.Combine(_mumuPath, "nx_main", "mumu-cli.exe");
        if (File.Exists(cliExe))
        {
            _mumuCliExe = cliExe;
            _isMuMu12 = true;
            return;
        }

        // 旧版 MuMu：杀 MuMuPlayer.exe 进程后重新启动
        var legacyExe = Path.Combine(_mumuPath, "MuMuPlayer.exe");
        if (File.Exists(legacyExe))
        {
            _mumuLegacyExe = legacyExe;
            _mumuProcessName = "MuMuPlayer";
            _isMuMu12 = false;
            return;
        }
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
        if (_isMuMu12)
        {
            RestartEmulatorViaCli();
        }
        else
        {
            RestartEmulatorLegacy();
        }
    }

    /// <summary>
    /// MuMu 12+：通过 mumu-cli.exe control restart 重启实例，无需杀进程
    /// </summary>
    private void RestartEmulatorViaCli()
    {
        if (string.IsNullOrWhiteSpace(_mumuCliExe))
        {
            LoggerHelper.Info("[RestartGameAction] 未找到 mumu-cli.exe，跳过模拟器重启");
            return;
        }

        LoggerHelper.Info($"[RestartGameAction] 通过 CLI 重启模拟器实例 {_mumuIndex}...");
        var restartPsi = new ProcessStartInfo(_mumuCliExe, $"control --vmindex {_mumuIndex} restart")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        var proc = Process.Start(restartPsi);
        proc?.WaitForExit(30000);

        // 等待 ADB 重新连接
        LoggerHelper.Info("[RestartGameAction] 等待模拟器启动...");
        Thread.Sleep(10000);
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
            var checkProc = Process.Start(checkPsi);
            checkProc?.WaitForExit(5000);
            if (checkProc?.ExitCode == 0)
            {
                LoggerHelper.Info("[RestartGameAction] 模拟器已就绪");
                return;
            }
            Thread.Sleep(2000);
        }
        LoggerHelper.Info("[RestartGameAction] 模拟器启动超时，继续后续流程");
    }

    /// <summary>
    /// 旧版 MuMu：杀 MuMuPlayer.exe 进程后重新启动
    /// </summary>
    private void RestartEmulatorLegacy()
    {
        if (string.IsNullOrWhiteSpace(_mumuLegacyExe) || string.IsNullOrWhiteSpace(_mumuProcessName))
        {
            LoggerHelper.Info("[RestartGameAction] 未找到 MuMu 主程序，跳过模拟器重启");
            return;
        }

        LoggerHelper.Info($"[RestartGameAction] 正在关闭模拟器（{_mumuProcessName}）...");
        var killPsi = new ProcessStartInfo("taskkill", $"/F /IM {_mumuProcessName}.exe /T")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        Process.Start(killPsi)?.WaitForExit(5000);
        Thread.Sleep(3000);

        LoggerHelper.Info($"[RestartGameAction] 正在启动模拟器（{_mumuLegacyExe}）...");
        var startArgs = _mumuIndex > 0 ? $"-v {_mumuIndex}" : "";
        var startPsi = new ProcessStartInfo(_mumuLegacyExe, startArgs)
        {
            UseShellExecute = true,
        };
        Process.Start(startPsi);

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
            var checkProc = Process.Start(checkPsi);
            checkProc?.WaitForExit(5000);
            if (checkProc?.ExitCode == 0)
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

            // 1. 强制停止游戏进程，确保从卡死状态恢复
            LoggerHelper.Info($"[RestartGameAction] 强制停止游戏进程: {package}");
            var forceStopPsi = new ProcessStartInfo(_adbPath!, $"shell am force-stop {package}")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };
            if (!string.IsNullOrWhiteSpace(_adbSerial))
                forceStopPsi.Arguments = $"-s {_adbSerial} shell am force-stop {package}";
            Process.Start(forceStopPsi)?.WaitForExit(5000);
            Thread.Sleep(2000);

            // 2. 重新启动游戏
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
