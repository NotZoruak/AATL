using SharpHook;
using SharpHook.Data;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MFAAvalonia.Helper.HotkeyIpc;

/// <summary>
/// IPC 消息类型枚举
/// </summary>
public enum HotkeyMessageType
{
    RegisterHotkey,
    UnregisterHotkey,
    Heartbeat,
    HeartbeatAck,
    HotkeyTriggered,
    PrimaryChanged,
    Connect,
    ConnectAck,
    Disconnect,
    SyncRequest,
    SyncResponse,
    ElectionRequest,
    ElectionResponse,
    ElectionVictory
}

/// <summary>
/// 热键标识符
/// </summary>
public class HotkeyIdentifier
{
    public KeyCode KeyCode { get; set; }
    public EventMask Modifiers { get; set; }

    public HotkeyIdentifier() { }

    public HotkeyIdentifier(KeyCode keyCode, EventMask modifiers)
    {
        KeyCode = keyCode;
        Modifiers = modifiers;
    }

    public override bool Equals(object? obj)
    {
        if (obj is HotkeyIdentifier other)
            return KeyCode == other.KeyCode && Modifiers == other.Modifiers;
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(KeyCode, Modifiers);
    
    public override string ToString() => $"{Modifiers}+{KeyCode}";
}

/// <summary>
/// IPC 消息
/// </summary>
public class HotkeyMessage
{
    public HotkeyMessageType Type { get; set; }
    public int SenderId { get; set; }
    public long Timestamp { get; set; }
    public long SequenceNumber { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HotkeyIdentifier? Hotkey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HotkeyIdentifier[]? Hotkeys { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int NewPrimaryId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long ElectionPriority { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientId { get; set; }

    private static long _sequenceCounter = 0;
    private static readonly object _sequenceLock = new();

    public HotkeyMessage()
    {
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SenderId = Environment.ProcessId;
    }

    public static HotkeyMessage Create(HotkeyMessageType type)
    {
        lock (_sequenceLock)
        {
            return new HotkeyMessage { Type = type, SequenceNumber = ++_sequenceCounter };
        }
    }

    public static HotkeyMessage CreateRegister(KeyCode keyCode, EventMask modifiers)
    {
        var msg = Create(HotkeyMessageType.RegisterHotkey);
        msg.Hotkey = new HotkeyIdentifier(keyCode, modifiers);
        return msg;
    }

    public static HotkeyMessage CreateUnregister(KeyCode keyCode, EventMask modifiers)
    {
        var msg = Create(HotkeyMessageType.UnregisterHotkey);
        msg.Hotkey = new HotkeyIdentifier(keyCode, modifiers);
        return msg;
    }

    public static HotkeyMessage CreateTriggered(KeyCode keyCode, EventMask modifiers)
    {
        var msg = Create(HotkeyMessageType.HotkeyTriggered);
        msg.Hotkey = new HotkeyIdentifier(keyCode, modifiers);
        return msg;
    }

    public static HotkeyMessage CreateHeartbeat() => Create(HotkeyMessageType.Heartbeat);

    public static HotkeyMessage CreateConnect(string clientId)
    {
        var msg = Create(HotkeyMessageType.Connect);
        msg.ClientId = clientId;
        return msg;
    }

    public static HotkeyMessage CreateElectionRequest(long priority)
    {
        var msg = Create(HotkeyMessageType.ElectionRequest);
        msg.ElectionPriority = priority;
        return msg;
    }

    public static HotkeyMessage CreateElectionVictory()
    {
        var msg = Create(HotkeyMessageType.ElectionVictory);
        msg.NewPrimaryId = Environment.ProcessId;
        return msg;
    }

    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);

    /// <summary>
    /// 序列化为 JSON 字符串，使用统一的序列化选项
    /// </summary>
    public string SerializeToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static HotkeyMessage? Deserialize(byte[] data)
    {
        try { return JsonSerializer.Deserialize<HotkeyMessage>(data, JsonOptions); }
        catch { return null; }
    }

    public static HotkeyMessage? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<HotkeyMessage>(json, JsonOptions); }
        catch { return null; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}