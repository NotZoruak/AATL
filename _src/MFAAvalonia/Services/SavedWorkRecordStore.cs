using MFAAvalonia.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MFAAvalonia.Services;

/// <summary>已保存工作记录的本地 JSON 存储。</summary>
public static class SavedWorkRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static List<SavedWorkRecord> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];
            return JsonSerializer.Deserialize<List<SavedWorkRecord>>(File.ReadAllText(path), JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(string path, IEnumerable<SavedWorkRecord> records)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(records.ToList(), JsonOptions);
        File.WriteAllText(path, json);
    }
}
