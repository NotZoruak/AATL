using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace MFAAvalonia.Extensions.MaaFW;

/// <summary>
/// 可执行文件路径查找工具类
/// </summary>
public static class PathFinder
{
    /// <summary>
    /// 查找可执行文件路径
    /// </summary>
    /// <param name="fileName">文件名（不含扩展名）</param>
    /// <returns>找到的完整路径，如果未找到则返回原始文件名</returns>
    public static string FindPath(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return FindOnWindows(fileName);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return FindOnMacOS(fileName);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return FindOnLinux(fileName);
        }
        else
        {
            return FindGeneric(fileName);
        }
    }

    private static string FindOnWindows(string fileName)
    {
        // Windows 可执行文件扩展名
        var extensions = new[] { ".exe", ".cmd", ".bat", ".com", "" };
        
        // 先检查 PATH 环境变量
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                foreach (var ext in extensions)
                {
                    try
                    {
                        var fullPath = Path.Combine(dir, $"{fileName}{ext}");
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                    catch
                    {
                        /* 忽略错误目录 */
                    }
                }
            }
        }

        return fileName;
    }

    private static string FindOnMacOS(string fileName)
    {
        // 检查 PATH 环境变量
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var fullPath = Path.Combine(dir, fileName);
                    if (File.Exists(fullPath) && IsExecutable(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    /* 忽略错误目录 */
                }
            }
        }

        // 检查常见的安装位置
        var commonDirs = new[]
        {
            "/usr/local/bin",
            "/usr/bin",
            "/opt/homebrew/bin"
        };

        foreach (var dir in commonDirs)
        {
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath) && IsExecutable(fullPath))
            {
                return fullPath;
            }
        }

        return fileName;
    }

    private static string FindOnLinux(string fileName)
    {
        // 检查 PATH 环境变量
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var fullPath = Path.Combine(dir, fileName);
                    if (File.Exists(fullPath) && IsExecutable(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    /* 忽略错误目录 */
                }
            }
        }

        // 检查常见的安装位置
        var commonDirs = new[]
        {
            "/usr/bin",
            "/usr/local/bin",
            "/opt/bin"
        };

        foreach (var dir in commonDirs)
        {
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath) && IsExecutable(fullPath))
            {
                return fullPath;
            }
        }

        return fileName;
    }

    private static string FindGeneric(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var fullPath = Path.Combine(dir, fileName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch
                {
                    /* 忽略错误目录 */
                }
            }
        }

        return fileName;
    }

    /// <summary>
    /// 检查文件是否具有可执行权限 (仅适用于 Unix-like 系统)
    /// </summary>
    private static bool IsExecutable(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "file",
                Arguments = $"--brief --mime-type \"{path}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process != null)
            {
                process.WaitForExit();
                var output = process.StandardOutput.ReadToEnd().Trim();
                return output.Contains("executable") || output.Contains("application/x-executable");
            }

            return false;
        }
        catch
        {
            return File.Exists(path);
        }
    }
}
