using MFAAvalonia.Models;

namespace MFAAvalonia.Configuration;

/// <summary>仓库识别服务测试所需的最小配置访问替身。</summary>
public static class ConfigurationManager
{
    public static TestConfiguration Current { get; } = new();
}

public sealed class TestConfiguration
{
    public T GetValue<T>(string _, T fallback) => fallback;
}

public static class ConfigurationKeys
{
    public const string WarehouseData = "WarehouseData";
}
