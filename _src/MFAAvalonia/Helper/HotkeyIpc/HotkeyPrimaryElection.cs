using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace MFAAvalonia.Helper.HotkeyIpc;

/// <summary>
/// 主进程选举管理器 - 处理主进程选举和故障转移
/// 使用全局互斥锁（不带目录路径），确保所有 MFAAvalonia 实例中只有一个注册全局钩子
/// </summary>
public class HotkeyPrimaryElection : IDisposable
{
    private const string StateFileName = "hotkey_state.json";
    private const string PipeName = "MFAAvalonia_HotkeyIpc";
    // 全局互斥锁名称 - 不包含目录路径，对所有 MFAAvalonia 实例生效
    private const string GlobalMutexName = "Global\\MFAAvalonia_HotkeyPrimary";

    private Mutex? _primaryMutex;
    private bool _isPrimary;
    private bool _disposed;
    private readonly long _startTime;

    public bool IsPrimary => _isPrimary;
    public long StartTime => _startTime;

    public event Action? BecamePrimary;
    public event Action? LostPrimary;

    public HotkeyPrimaryElection()
    {
        _startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 尝试成为主进程
    /// </summary>
    public bool TryBecomePrimary()
    {
        if (_isPrimary) return true;

        try
        {
            // 使用 Global\ 前缀确保跨所有会话和用户的全局互斥锁
            _primaryMutex = new Mutex(true, GlobalMutexName, out bool createdNew);

            if (createdNew)
            {
                _isPrimary = true;
                LoggerHelper.Info("热键主进程选举：已成为主进程（创建了新的全局互斥锁）。");
                BecamePrimary?.Invoke();
                return true;
            }
            else
            {
                // 互斥锁已存在，尝试获取
                try
                {
                    bool acquired = _primaryMutex.WaitOne(0);
                    if (acquired)
                    {
                        _isPrimary = true;
                        LoggerHelper.Info("热键主进程选举：已成为主进程（获取了现有互斥锁）。");
                        BecamePrimary?.Invoke();
                        return true;
                    }
                }
                catch (AbandonedMutexException)
                {
                    // 之前的主进程崩溃，我们获得了互斥锁
                    _isPrimary = true;
                    LoggerHelper.Warning("热键主进程选举：检测到废弃互斥锁，已接管主进程角色。");
                    BecamePrimary?.Invoke();
                    return true;
                }

                // 无法获取互斥锁，释放资源
                _primaryMutex.Dispose();
                _primaryMutex = null;
                LoggerHelper.Info("热键主进程选举：已有其他实例持有互斥锁，当前实例不作为主进程。");
                return false;
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"热键主进程选举失败：原因={ex.Message}");
            _primaryMutex?.Dispose();
            _primaryMutex = null;
            return false;
        }
    }

    /// <summary>
    /// 释放主进程角色
    /// </summary>
    public void ReleasePrimary()
    {
        if (!_isPrimary) return;

        try
        {
            _primaryMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // 当前线程不拥有互斥锁
        }

        _primaryMutex?.Dispose();
        _primaryMutex = null;
        _isPrimary = false;
        LostPrimary?.Invoke();
        LoggerHelper.Info("热键主进程选举：已释放主进程角色。");
    }

    /// <summary>
    /// 检查主进程是否存活（通过尝试连接 TCP 端口）
    /// </summary>
    public static bool IsPrimaryAlive()
    {
        // 首先尝试从文件读取端口
        int? savedPort = HotkeyIpcServer.LoadPortFromFile();
        if (savedPort.HasValue)
        {
            if (TryConnectToPort(savedPort.Value))
                return true;
        }

        // 扫描端口范围
        const int DefaultPort = 52718;
        const int PortRange = 10;
        for (int i = 0; i < PortRange; i++)
        {
            if (TryConnectToPort(DefaultPort + i))
                return true;
        }

        return false;
    }

    private static bool TryConnectToPort(int port)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect("127.0.0.1", port, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
            if (success && client.Connected)
            {
                client.EndConnect(result);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 保存热键状态到文件（用于故障转移时恢复）
    /// </summary>
    public static void SaveState(HotkeyIdentifier[] hotkeys)
    {
        try
        {
            var statePath = GetStatePath();
            var json = System.Text.Json.JsonSerializer.Serialize(hotkeys);
            File.WriteAllText(statePath, json);
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"保存热键状态失败：原因={ex.Message}");
        }
    }

    /// <summary>
    /// 加载热键状态（新主进程使用）
    /// </summary>
    public static HotkeyIdentifier[]? LoadState()
    {
        try
        {
            var statePath = GetStatePath();
            if (!File.Exists(statePath)) return null;

            var json = File.ReadAllText(statePath);
            return System.Text.Json.JsonSerializer.Deserialize<HotkeyIdentifier[]>(json);
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"加载热键状态失败：原因={ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 清除状态文件
    /// </summary>
    public static void ClearState()
    {
        try
        {
            var statePath = GetStatePath();
            if (File.Exists(statePath))
                File.Delete(statePath);
        }
        catch { }
    }

    private static string GetStatePath()
    {
        var appData = AppPaths.ConfigDirectory;
        Directory.CreateDirectory(appData);
        return Path.Combine(appData, StateFileName);
    }  

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleasePrimary();
    }
}
