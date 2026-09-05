using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MaaFramework.Binding;
using MFAAvalonia.Extensions.MaaFW;

namespace MFAAvalonia.Helper;

/// <summary>
/// MaaCore 日志(debug/maafw.log)运行期切块器。
/// 利用 MaaCore 官方 API SetOption_LogDir 运行期切换日志目录,
/// 切换后旧文件句柄关闭,即可安全移动为 bak,实现按大小切块。
/// </summary>
public static class MaaLogRotator
{
    // 切块阈值:20 MiB
    public const long MaxLogChunkBytes = 20L * 1024 * 1024;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private static CancellationTokenSource? _cts;
    private static Task? _pollTask;

    // 当前 MaaCore 日志目录。正常情况下为 debug 根目录(null 表示默认);
    // 若切回原目录失败,记录切块临时目录,使轮询能继续追踪该目录下的日志,避免失去切块控制。
    private static string? _activeLogDir;

    // 切块互斥闸门:轮询与停止兜底可能并发进入切块,只允许一个执行,其余直接跳过,避免目录切换交错
    private static readonly SemaphoreSlim _rotateGate = new(1, 1);

    /// <summary>
    /// 启动后台轮询切块。应用启动时调用一次,幂等。
    /// </summary>
    public static void Start()
    {
        if (_pollTask is { IsCompleted: false })
            return;

        var cancellationTokenSource = new CancellationTokenSource();
        _cts = cancellationTokenSource;
        _pollTask = Task.Run(async () =>
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    TryRotateIfNeeded();
                }
                catch (Exception ex)
                {
                    // 轮询异常不影响任务运行,仅记录
                    LoggerHelper.Warning($"日志切块轮询异常: {ex.Message}");
                }

                try
                {
                    await Task.Delay(PollInterval, cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cancellationTokenSource.Token);
    }

    /// <summary>
    /// 停止后台轮询切块。应用退出时调用，避免遗留后台任务。
    /// </summary>
    public static void Stop()
    {
        var cancellationTokenSource = Interlocked.Exchange(ref _cts, null);
        _pollTask = null;
        cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// 检查 debug/maafw.log 是否超过阈值,超过则执行一次切块。
    /// 轮询与任务停止兜底共用;任何异常均内部消化,不向外抛出。
    /// </summary>
    public static void TryRotateIfNeeded()
    {
        var debugDir = Path.Combine(AppPaths.InstallRoot, "debug");
        var activeDir = _activeLogDir ?? debugDir;
        var mainLog = Path.Combine(activeDir, "maafw.log");

        if (!File.Exists(mainLog))
            return;

        if (new FileInfo(mainLog).Length <= MaxLogChunkBytes)
            return;

        var tasker = MaaProcessorManager.Instance.Current.MaaTasker;
        if (tasker == null)
            return;

        // 串行化切块:并发调用只允许一个执行,其余直接跳过,避免目录切换交错
        if (!_rotateGate.Wait(0))
        {
            LoggerHelper.Warning("日志切块: 上一次切块尚未完成,本次跳过");
            return;
        }

        try
        {
            RotateCore(tasker, debugDir, mainLog);
        }
        finally
        {
            _rotateGate.Release();
        }
    }

    /// <summary>
    /// 执行切块:切换日志目录 → 移动旧日志 → 切回原目录 → 保留过渡日志。
    /// </summary>
    private static void RotateCore(MaaFramework.Binding.MaaTasker tasker, string debugDir, string mainLog)
    {
        var rotateDir = Path.Combine(AppPaths.TempMaaFwDirectory, $"rotate_{DateTime.Now:yyyyMMdd_HHmmss_fff}");
        // 记录切块阶段状态,用于异常时保持 MaaCore 实际日志目录与 _activeLogDir 一致
        var switched = false;      // 是否已成功切到临时目录
        var switchedBack = false;  // 是否已成功切回原目录

        try
        {
            Directory.CreateDirectory(rotateDir);

            LoggerHelper.Info($"日志切块: 主日志 {new FileInfo(mainLog).Length} 字节, 切换日志目录至 {rotateDir}");

            // 1. 切到临时目录,使 MaaCore 关闭旧文件句柄
            if (!tasker.Global.SetOption_LogDir(rotateDir))
            {
                LoggerHelper.Warning("日志切块失败: SetOption_LogDir 切换到临时目录失败");
                return;
            }
            switched = true;

            // 2. 移动旧日志为 bak(此刻旧句柄已关闭,移动必然成功),记录主 bak 路径供过渡日志合并
            var mainBakPath = MoveToBak(mainLog, debugDir);

            // 3. 切回原目录,后续日志继续写 debug/maafw.log
            if (tasker.Global.SetOption_LogDir(debugDir))
            {
                switchedBack = true;
                _activeLogDir = null;

                // 4. 合并过渡日志(临时目录中的新文件,此时仅几行)进主 bak 末尾:
                // 过渡日志内容的时间顺序恰在主日志之后,追加后 bak 内容天然连续,
                // 避免每次切块在 debug 目录产生独立的小碎 bak 文件;
                // 复制与删除任一步失败仅警告,不中止、不抛出(维持现状的容错语义)
                var transitionLog = Path.Combine(rotateDir, "maafw.log");
                if (File.Exists(transitionLog) && new FileInfo(transitionLog).Length > 0)
                {
                    try
                    {
                        // 追加流:共享读权限,避免与其他句柄冲突
                        using (var appendStream = new FileStream(mainBakPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                        using (var sourceStream = new FileStream(transitionLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            sourceStream.CopyTo(appendStream);
                        }
                        File.Delete(transitionLog);
                        LoggerHelper.Info($"日志切块: 过渡日志已合并进 {Path.GetFileName(mainBakPath)}");
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.Warning($"日志切块: 合并过渡日志失败: {ex.Message}");
                    }
                }
            }
            else
            {
                // 切回失败:记录当前日志目录,轮询继续追踪 rotateDir 下的日志,保持切块能力;
                // 过渡日志(rotateDir/maafw.log)保留原处,由轮询在下次切块中一并处理
                _activeLogDir = rotateDir;
                LoggerHelper.Warning($"日志切块: 切回日志目录 {debugDir} 失败,已记录当前日志目录 {rotateDir}");
            }

            LoggerHelper.Info("日志切块完成");
        }
        catch (Exception ex)
        {
            // 切块失败不影响任务运行,仅记录;原日志文件若未移动则保留原样
            LoggerHelper.Warning($"日志切块失败: {ex.Message}");

            // 若已切到临时目录且尚未切回:MaaCore 仍写 rotateDir,
            // 记录该目录使轮询继续追踪,避免切块功能静默失效;
            // 切回成功后出现的异常(如过渡日志移动失败)不再覆盖 _activeLogDir,
            // 此时 MaaCore 实际已在 debugDir,轮询应继续追踪 debugDir
            if (switched && !switchedBack)
            {
                _activeLogDir = rotateDir;
            }
        }
        finally
        {
            try
            {
                // 仅当 rotateDir 不是当前追踪目录(避免删除 MaaCore 正在写入的目录)且已空时清理
                if (Directory.Exists(rotateDir)
                    && !string.Equals(_activeLogDir, rotateDir, StringComparison.Ordinal)
                    && !Directory.EnumerateFileSystemEntries(rotateDir).Any())
                {
                    Directory.Delete(rotateDir);
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"清理切块临时目录失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 移动日志文件为 bak 到指定目录,命名沿用现有格式(与 AppPaths.CleanupOldDebugLogs 一致),返回 bak 路径。
    /// </summary>
    private static string MoveToBak(string sourcePath, string targetDir)
    {
        var timestamp = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss.fff");
        var bakPath = Path.Combine(targetDir, $"maafw.bak.{timestamp}.log");
        // 同毫秒内多次移动时追加序号,避免目标已存在导致 IOException
        for (var i = 2; File.Exists(bakPath); i++)
        {
            bakPath = Path.Combine(targetDir, $"maafw.bak.{timestamp}_{i}.log");
        }
        File.Move(sourcePath, bakPath);
        LoggerHelper.Info($"日志切块: {Path.GetFileName(sourcePath)} → {Path.GetFileName(bakPath)}");
        return bakPath;
    }
}
