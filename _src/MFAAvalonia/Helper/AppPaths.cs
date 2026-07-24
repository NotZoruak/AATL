using System;
using System.IO;
using System.Linq;

namespace MFAAvalonia.Helper;

public static class AppPaths
{
    private static bool _initialized;
    private static string _configDirectory = string.Empty;
    private static string _logsDirectory = string.Empty;
    private static string _tempDirectory = string.Empty;

    public static string InstallRoot => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    public static string DataRoot { get; private set; } = InstallRoot;
    public static string ConfigDirectory => string.IsNullOrWhiteSpace(_configDirectory)
        ? Path.Combine(InstallRoot, "config")
        : _configDirectory;
    public static string InstancesDirectory => Path.Combine(ConfigDirectory, "instances");
    public static string ResourceDirectory => Path.Combine(DataRoot, "resource");
    public static string AgentDirectory => Path.Combine(DataRoot, "agent");
    public static string LogsDirectory => string.IsNullOrWhiteSpace(_logsDirectory)
        ? Path.Combine(InstallRoot, "debug", "logs")
        : _logsDirectory;
    public static string TempDirectory => string.IsNullOrWhiteSpace(_tempDirectory)
        ? Path.Combine(InstallRoot, "temp")
        : _tempDirectory;
    public static string TempResourceDirectory => Path.Combine(TempDirectory, "temp_res");
    public static string TempMfaDirectory => Path.Combine(TempDirectory, "temp_mfa");
    public static string TempMaaFwDirectory => Path.Combine(TempDirectory, "temp_maafw");
    public static string InterfaceJsonPath => Path.Combine(DataRoot, "interface.json");
    public static string InterfaceJsoncPath => Path.Combine(DataRoot, "interface.jsonc");
    public static string GlobalConfigPath => Path.Combine(DataRoot, "appsettings.json");
    public static string ChangesPath => Path.Combine(DataRoot, "changes.json");
    public static string BackupDirectory => Path.Combine(DataRoot, "backup");
    public static bool IsUsingIndependentDataRoot => !string.Equals(DataRoot, InstallRoot, StringComparison.OrdinalIgnoreCase);
    public static bool IsUsingLocalConfigDirectory => string.Equals(
        ConfigDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        Path.Combine(InstallRoot, "config").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase);

    public static void Initialize()
    {
        if (_initialized)
            return;

        DataRoot = InstallRoot;
        _configDirectory = ResolveConfigDirectory();
        _logsDirectory = ResolveLogsDirectory();
        _tempDirectory = ResolveTempDirectory();

        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(InstancesDirectory);
        Directory.CreateDirectory(ResourceDirectory);
        Directory.CreateDirectory(AgentDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(BackupDirectory);

        LogInitializationStatus();

        _initialized = true;
    }

    private static string ResolveConfigDirectory()
    {
        var installConfig = Path.Combine(InstallRoot, "config");
        if (CanUseDirectory(installConfig))
            return installConfig;

        return installConfig;
    }

    private static string ResolveLogsDirectory()
    {
        var installLogs = Path.Combine(InstallRoot, "debug", "logs");
        if (CanUseDirectory(installLogs))
            return installLogs;

        return installLogs;
    }

    private static string ResolveTempDirectory()
    {
        var installTemp = Path.Combine(InstallRoot, "temp");
        if (CanUseDirectory(installTemp))
            return installTemp;

        return installTemp;
    }

    private static bool CanUseDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probePath = Path.Combine(path, ".write_probe");
            File.WriteAllText(probePath, DateTime.UtcNow.ToString("O"));
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LogInitializationStatus()
    {
        try
        {
            LoggerHelper.Info($"应用安装目录：{InstallRoot}");
            LoggerHelper.Info($"应用数据目录：{DataRoot}");
            LoggerHelper.Info($"是否使用独立数据目录：{IsUsingIndependentDataRoot}");
            LoggerHelper.Info($"配置目录：{ConfigDirectory}");
            LoggerHelper.Info($"是否使用本地配置目录：{IsUsingLocalConfigDirectory}");
            LoggerHelper.Info($"日志目录：{LogsDirectory}");

            var backupCount = Directory.Exists(DataRoot)
                ? Directory.EnumerateFiles(DataRoot, "*.backupMFA", SearchOption.AllDirectories).Count()
                : 0;
            var tempDirExists = Directory.Exists(TempDirectory);
            LoggerHelper.Info($"备份文件数量：{backupCount}");
            LoggerHelper.Info($"临时目录是否存在：{tempDirExists}");
        }
        catch
        {
            // Avoid breaking startup for diagnostics.
        }
    }

    public static void CleanupObsoleteExecutableBackups(Action<string>? logInfo = null, Action<string>? logWarning = null)
    {
        try
        {
            if (!Directory.Exists(InstallRoot))
                return;

            // 清理根目录下的 backupMFA 文件
            foreach (var backupFile in Directory.EnumerateFiles(InstallRoot, "*.backupMFA", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.SetAttributes(backupFile, FileAttributes.Normal);
                    File.Delete(backupFile);
                    logInfo?.Invoke($"已清理 backupMFA 文件：文件={backupFile}");
                }
                catch (Exception ex)
                {
                    logWarning?.Invoke($"清理 backupMFA 文件失败：文件={backupFile}，原因={ex.Message}");
                }
            }

            // 清理 libs 子目录下的 backupMFA 文件（仅保留每个 DLL 的最新备份）
            var libsPath = Path.Combine(InstallRoot, "runtimes/libs");
            if (Directory.Exists(libsPath))
            {
                var backupGroups = Directory.EnumerateFiles(libsPath, "*.backupMFA")
                    .GroupBy(f =>
                    {
                        var fileName = Path.GetFileName(f);
                        // 文件名格式: <dll>.<timestamp>.backupMFA，提取 DLL 名
                        var parts = fileName.Split('.');
                        return parts.Length > 0 ? parts[0] : fileName;
                    });

                foreach (var group in backupGroups)
                {
                    // 按文件名降序排列（时间戳越大越新），跳过最新的，删除其余
                    var ordered = group.OrderByDescending(f => f).ToList();
                    for (var i = 1; i < ordered.Count; i++)
                    {
                        try
                        {
                            File.SetAttributes(ordered[i], FileAttributes.Normal);
                            File.Delete(ordered[i]);
                        }
                        catch (Exception ex)
                        {
                            logWarning?.Invoke($"清理 libs backupMFA 失败：文件={ordered[i]}，原因={ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logWarning?.Invoke($"处理旧主程序 backupMFA 清理失败：原因={ex.Message}");
        }
    }

    public static void CleanupOldDebugLogs(int retainDays = 3, Action<string>? logInfo = null, Action<string>? logWarning = null)
    {
        try
        {
            if (!Directory.Exists(InstallRoot))
                return;

            var debugPath = Path.Combine(InstallRoot, "debug");
            if (!Directory.Exists(debugPath))
                return;

            var cutoff = DateTime.Now.AddDays(-retainDays);

            // 轮转主日志 maafw.log（启动时重命名为备份，MaaFramework 会自动创建新的）
            var mainLog = Path.Combine(debugPath, "maafw.log");
            if (File.Exists(mainLog) && new FileInfo(mainLog).Length > 0)
            {
                try
                {
                    var bakName = $"maafw.bak.{DateTime.Now:yyyyMMdd_HHmmss}.log";
                    File.Move(mainLog, Path.Combine(debugPath, bakName));
                    logInfo?.Invoke($"已轮转调试日志：{bakName}");
                }
                catch (Exception ex)
                {
                    logWarning?.Invoke($"轮转调试日志失败：原因={ex.Message}");
                }
            }

            // 清理过期的备份日志
            foreach (var file in Directory.EnumerateFiles(debugPath, "maafw.bak.*.log", SearchOption.AllDirectories))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        logInfo?.Invoke($"已清理过期调试日志：文件={Path.GetFileName(file)}");
                    }
                }
                catch (Exception ex)
                {
                    logWarning?.Invoke($"清理调试日志失败：文件={file}，原因={ex.Message}");
                }
            }

            // 清理过期的调试截图
            foreach (var pattern in new[] { "*.png", "*.jpg", "*.jpeg" })
            {
                foreach (var file in Directory.EnumerateFiles(debugPath, pattern, SearchOption.AllDirectories))
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff)
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        logWarning?.Invoke($"清理调试截图失败：文件={file}，原因={ex.Message}");
                    }
                }
            }
            logInfo?.Invoke("调试文件清理完成");
        }
        catch (Exception ex)
        {
            logWarning?.Invoke($"清理调试日志异常：原因={ex.Message}");
        }
    }
}
