using System;
using System.Collections.Generic;

namespace MFAAvalonia.Models;

/// <summary>仓库中的核心资源快照。</summary>
public sealed class WarehouseResourceSnapshot
{
    public DateTime RecordedAt { get; set; }
    public Dictionary<string, int> Values { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>仓库中一次识别得到的完整数据。</summary>
public sealed class WarehouseData
{
    public Dictionary<string, int> CoreResources { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> OtherItems { get; set; } = new(StringComparer.Ordinal);
    public List<WarehouseResourceSnapshot> ResourceHistory { get; set; } = [];

    public WarehouseData Clone()
    {
        return new WarehouseData
        {
            CoreResources = new Dictionary<string, int>(CoreResources, StringComparer.Ordinal),
            OtherItems = new Dictionary<string, int>(OtherItems, StringComparer.Ordinal),
            ResourceHistory = [.. ResourceHistory.ConvertAll(snapshot => new WarehouseResourceSnapshot
            {
                RecordedAt = snapshot.RecordedAt,
                Values = new Dictionary<string, int>(snapshot.Values, StringComparer.Ordinal),
            })],
        };
    }
}
