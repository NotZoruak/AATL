using MFAAvalonia.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MFAAvalonia.Services;

/// <summary>处理仓库自动识别产生的临时草稿。</summary>
public static class WarehouseScanDraftService
{
    private static readonly Dictionary<string, string> OtherItemNameCorrections = new(StringComparer.Ordinal)
    {
        ["锣·月下梅树透图碎片"] = "锷·月下梅树透图碎片",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>将 OCR 文本解析为资源数量。</summary>
    public static bool TryParseCount(string? text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.Trim()
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("，", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out value) && value >= 0;
    }

    /// <summary>读取临时草稿；文件不存在或内容损坏时返回空草稿。</summary>
    public static WarehouseData Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new WarehouseData();

            var draft = JsonSerializer.Deserialize<WarehouseScanDraft>(File.ReadAllText(path), JsonOptions);
            return draft?.ToWarehouseData() ?? new WarehouseData();
        }
        catch
        {
            return new WarehouseData();
        }
    }

    /// <summary>更新一个核心资源，并以原子方式写回草稿。</summary>
    public static void UpdateCoreResource(string path, string resource, int value)
    {
        var data = Load(path);
        data.CoreResources[resource] = Math.Max(0, value);
        Save(path, data);
    }

    /// <summary>更新一个其他物品，并保留草稿中的其他识别结果。</summary>
    public static void UpdateOtherItem(string path, string item, int value)
    {
        var data = Load(path);
        var normalizedItem = NormalizeOtherItemName(item);
        foreach (var alias in data.OtherItems.Keys
                     .Where(key => !string.Equals(key, normalizedItem, StringComparison.Ordinal)
                                && string.Equals(NormalizeOtherItemName(key), normalizedItem, StringComparison.Ordinal))
                     .ToList())
            data.OtherItems.Remove(alias);
        data.OtherItems.Remove(item);
        data.OtherItems[normalizedItem] = Math.Max(0, value);
        Save(path, data);
    }

    /// <summary>修正已知的其他物品 OCR 误识别名称。</summary>
    public static string NormalizeOtherItemName(string item)
    {
        if (item.Contains("月下梅树", StringComparison.Ordinal))
            return "锷·月下梅树透图碎片";

        return OtherItemNameCorrections.TryGetValue(item, out var corrected) ? corrected : item;
    }

    /// <summary>在完整识别结束时追加一次核心资源历史快照。</summary>
    public static void AppendSnapshot(string path, IDictionary<string, int> values, DateTime? recordedAt = null)
    {
        var data = Load(path);
        data.ResourceHistory.Add(new WarehouseResourceSnapshot
        {
            RecordedAt = recordedAt ?? DateTime.Now,
            Values = new Dictionary<string, int>(values, StringComparer.Ordinal),
        });
        Save(path, data);
    }

    /// <summary>删除上一次识别留下的临时草稿。</summary>
    public static void Clear(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>保存草稿，避免程序中断时留下半份 JSON。</summary>
    public static void Save(string path, WarehouseData data)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        var draft = WarehouseScanDraft.FromWarehouseData(data);
        File.WriteAllText(tempPath, JsonSerializer.Serialize(draft, JsonOptions));
        File.Move(tempPath, path, true);
    }

    private sealed class WarehouseScanDraft
    {
        [JsonPropertyName("core_resources")]
        public Dictionary<string, int> CoreResources { get; set; } = new(StringComparer.Ordinal);

        [JsonPropertyName("other_items")]
        public Dictionary<string, int> OtherItems { get; set; } = new(StringComparer.Ordinal);

        [JsonPropertyName("resource_history")]
        public List<WarehouseResourceSnapshot> ResourceHistory { get; set; } = [];

        public static WarehouseScanDraft FromWarehouseData(WarehouseData data) => new()
        {
            CoreResources = new Dictionary<string, int>(data.CoreResources, StringComparer.Ordinal),
            OtherItems = new Dictionary<string, int>(data.OtherItems, StringComparer.Ordinal),
            ResourceHistory = [.. data.ResourceHistory.Select(snapshot => new WarehouseResourceSnapshot
            {
                RecordedAt = snapshot.RecordedAt,
                Values = new Dictionary<string, int>(snapshot.Values, StringComparer.Ordinal),
            })],
        };

        public WarehouseData ToWarehouseData() => new()
        {
            CoreResources = new Dictionary<string, int>(CoreResources ?? [], StringComparer.Ordinal),
            OtherItems = new Dictionary<string, int>(OtherItems ?? [], StringComparer.Ordinal),
            ResourceHistory = [.. (ResourceHistory ?? []).Select(snapshot => new WarehouseResourceSnapshot
            {
                RecordedAt = snapshot.RecordedAt,
                Values = new Dictionary<string, int>(snapshot.Values, StringComparer.Ordinal),
            })],
        };
    }
}
