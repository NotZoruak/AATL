using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.Helper.HotkeyIpc;

/// <summary>
/// 热键 IPC 客户端 - 子进程使用 (基于 TCP Socket)
/// </summary>
public class HotkeyIpcClient : IDisposable
{
    private const int DefaultPort = 52718;
    private const int PortRange = 10;
    private const int ConnectTimeoutMs = 2000; // 增加超时时间，确保有足够时间完成握手
    private const int HeartbeatIntervalMs = 5000;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private bool _disposed;
    private bool _handshakeCompleted;  // 新增：标记握手是否完成
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public string ClientId { get; }
    /// <summary>
    /// 检查是否已连接并完成握手。只有在 TCP 连接成功且收到 ConnectAck 后才返回 true。
    /// </summary>
    public bool IsConnected => _tcpClient?.Connected == true && _handshakeCompleted;

    public event Action<HotkeyIdentifier>? HotkeyTriggered;
    public event Action? Disconnected;
    public event Action<int>? PrimaryChanged;

    public HotkeyIpcClient()
    {
        ClientId = $"{Environment.ProcessId}_{Guid.NewGuid():N}";
    }

    public async Task<bool> ConnectAsync()
    {
        if (IsConnected) return true;

        try
        {
            _cts = new CancellationTokenSource();

            // 首先尝试从文件读取端口
            int? savedPort = HotkeyIpcServer.LoadPortFromFile();
            LoggerHelper.Info($"热键 IPC 客户端读取端口文件结果：端口={(savedPort?.ToString() ?? "null")}");
            if (savedPort.HasValue)
            {
                LoggerHelper.Info($"热键 IPC 客户端准备连接端口文件中的端口：端口={savedPort.Value}");
                var result = await TryConnectToPort(savedPort.Value);
                LoggerHelper.Info($"热键 IPC 客户端端口文件连接结果：端口={savedPort.Value}，成功={result}");
                if (result)
                    return true;
            }

            // 如果文件中的端口不可用，扫描端口范围
            LoggerHelper.Info($"热键 IPC 客户端开始扫描端口范围：范围={DefaultPort}-{DefaultPort + PortRange - 1}");
            for (int i = 0; i < PortRange; i++)
            {
                int port = DefaultPort + i;
                // 如果已经尝试过文件中的端口，跳过
                if (savedPort.HasValue && port == savedPort.Value)
                    continue;
                if (await TryConnectToPort(port))
                    return true;
            }

            LoggerHelper.Warning("热键 IPC 客户端无法连接到任何可用端口。");Cleanup();
            return false;
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"热键 IPC 客户端连接失败：异常类型={ex.GetType().Name}，原因={ex.Message}");
            Cleanup();
            return false;
        }
    }

    private async Task<bool> TryConnectToPort(int port)
    {
        TcpClient? tcpClient = null;
        try
        {
            LoggerHelper.Info($"热键 IPC 客户端尝试连接端口：端口={port}");
            
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;

            using var connectCts = new CancellationTokenSource(ConnectTimeoutMs);
            await tcpClient.ConnectAsync("127.0.0.1", port, connectCts.Token);

            if (!tcpClient.Connected)
            {
                LoggerHelper.Info($"热键 IPC 客户端端口连接后状态异常：端口={port}，状态=未连接");
                tcpClient.Dispose();
                return false;
            }

            LoggerHelper.Info($"热键 IPC 客户端 TCP 连接成功：端口={port}");
            _tcpClient = tcpClient;
            tcpClient = null; // 防止 finally 中被释放
            _stream = _tcpClient.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
            _writer = new StreamWriter(_stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            // 发送连接请求
            var connectMsg = HotkeyMessage.CreateConnect(ClientId);
            LoggerHelper.Info("热键 IPC 客户端已发送连接请求。");
            await WriteLineAsync(connectMsg.SerializeToJson());

            // 等待连接确认（带超时）
            LoggerHelper.Info("热键 IPC 客户端正在等待连接确认。");
            using var readCts = new CancellationTokenSource(ConnectTimeoutMs);
            var response = await _reader.ReadLineAsync(readCts.Token);
            
            if (string.IsNullOrEmpty(response))
            {
                LoggerHelper.Warning("热键 IPC 客户端连接确认响应为空。");
                CleanupConnection();
                return false;
            }

            LoggerHelper.Info($"热键 IPC 客户端收到响应：内容={response}");
            var ack = HotkeyMessage.Deserialize(response);
            if (ack?.Type != HotkeyMessageType.ConnectAck)
            {
                LoggerHelper.Warning($"热键 IPC 客户端连接确认类型错误：类型={ack?.Type}");
                CleanupConnection();
                return false;
            }

            // 标记握手完成
            _handshakeCompleted = true;
            
            // 启动接收和心跳任务
            _receiveTask = Task.Run(ReceiveLoopAsync);
            _heartbeatTask = Task.Run(HeartbeatLoopAsync);

            LoggerHelper.Info($"热键 IPC 客户端已连接到主进程：端口={port}，客户端ID={ClientId}");
            return true;
        }
        catch (OperationCanceledException)
        {
            LoggerHelper.Info($"热键 IPC 客户端连接端口超时：端口={port}");
            tcpClient?.Dispose();
            return false;
        }
        catch (SocketException ex)
        {
            LoggerHelper.Info($"热键 IPC 客户端端口连接被拒绝：端口={port}，SocketError={ex.SocketErrorCode}");
            tcpClient?.Dispose();
            return false;
        }
        catch (Exception ex)
        {
            LoggerHelper.Info($"热键 IPC 客户端连接端口失败：端口={port}，异常类型={ex.GetType().Name}，原因={ex.Message}");
            tcpClient?.Dispose();
            return false;
        }
    }

    private async Task WriteLineAsync(string message)
    {
        if (_writer == null) return;
        await _writeLock.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(message);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!_cts!.Token.IsCancellationRequested && _reader != null && IsConnected)
            {
                string? line;
                try
                {
                    line = await _reader.ReadLineAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }

                if (string.IsNullOrEmpty(line))
                {
                    LoggerHelper.Debug("热键 IPC 客户端收到空消息，连接已断开。");
                    break;
                }

                LoggerHelper.Debug($"热键 IPC 客户端收到消息：{line}");
                var msg = HotkeyMessage.Deserialize(line);
                if (msg == null) continue;

                switch (msg.Type)
                {
                    case HotkeyMessageType.HotkeyTriggered when msg.Hotkey != null:
                        LoggerHelper.Info($"热键 IPC 客户端收到热键触发：热键={msg.Hotkey}");
                        HotkeyTriggered?.Invoke(msg.Hotkey);
                        break;
                    case HotkeyMessageType.PrimaryChanged:
                        PrimaryChanged?.Invoke(msg.NewPrimaryId);
                        break;
                    case HotkeyMessageType.HeartbeatAck:
                        // 心跳响应，连接正常
                        LoggerHelper.Debug("热键 IPC 客户端收到心跳响应。");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"热键 IPC 客户端接收循环异常：原因={ex.Message}");
        }
        finally
        {
            LoggerHelper.Info("热键 IPC 客户端接收循环结束，准备触发断开事件。");
            Disconnected?.Invoke();
        }
    }

    private async Task HeartbeatLoopAsync()
    {
        try
        {
            while (!_cts!.Token.IsCancellationRequested && IsConnected)
            {
                await Task.Delay(HeartbeatIntervalMs, _cts.Token);
                if (_writer != null && IsConnected)
                {
                    var heartbeat = HotkeyMessage.CreateHeartbeat();
                    await WriteLineAsync(heartbeat.SerializeToJson());
                    LoggerHelper.Debug("热键 IPC 客户端已发送心跳。");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"热键 IPC 客户端心跳异常：原因={ex.Message}");
        }
    }

    public async Task RegisterHotkeyAsync(HotkeyIdentifier hotkey)
    {
        if (!IsConnected || _writer == null) return;
        try
        {
            var msg = HotkeyMessage.CreateRegister(hotkey.KeyCode, hotkey.Modifiers);
            await WriteLineAsync(msg.SerializeToJson());
            LoggerHelper.Info($"热键 IPC 客户端注册热键：热键={hotkey}");
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"热键 IPC 客户端注册热键失败：原因={ex.Message}");
        }
    }

    public async Task UnregisterHotkeyAsync(HotkeyIdentifier hotkey)
    {
        if (!IsConnected || _writer == null) return;
        try
        {
            var msg = HotkeyMessage.CreateUnregister(hotkey.KeyCode, hotkey.Modifiers);
            await WriteLineAsync(msg.SerializeToJson());
            LoggerHelper.Info($"热键 IPC 客户端注销热键：热键={hotkey}");
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"热键 IPC 客户端注销热键失败：原因={ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected || _writer == null) return;
        try
        {
            var msg = HotkeyMessage.Create(HotkeyMessageType.Disconnect);
            await WriteLineAsync(msg.SerializeToJson());
        }
        catch { }
        finally
        {
            Cleanup();
        }
    }

    private void CleanupConnection()
    {
        _handshakeCompleted = false;  // 重置握手状态
        _reader?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
        _reader = null;
        _writer = null;
        _stream = null;
        _tcpClient = null;
    }

    private void Cleanup()
    {
        _cts?.Cancel();
        CleanupConnection();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
        _cts?.Dispose();
        _writeLock.Dispose();
    }
}
