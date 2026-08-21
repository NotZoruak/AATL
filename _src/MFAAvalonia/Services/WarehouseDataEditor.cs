using MFAAvalonia.Models;
using System;
using System.Collections.Generic;

namespace MFAAvalonia.Services;

/// <summary>管理仓库页面的编辑状态与已保存状态。</summary>
public sealed class WarehouseDataEditor
{
    private WarehouseData _savedData;

    public WarehouseDataEditor(WarehouseData? data = null)
    {
        _savedData = (data ?? new WarehouseData()).Clone();
        Data = _savedData.Clone();
    }

    public WarehouseData Data { get; private set; }
    public bool HasChanges => !AreEqual(Data, _savedData);

    public void Save() => _savedData = Data.Clone();
    public void Revert() => Data = _savedData.Clone();
    public void Clear() => Data = new WarehouseData();

    private static bool AreEqual(WarehouseData left, WarehouseData right)
    {
        return DictionaryEquals(left.CoreResources, right.CoreResources)
            && DictionaryEquals(left.OtherItems, right.OtherItems)
            && left.ResourceHistory.Count == right.ResourceHistory.Count;
    }

    private static bool DictionaryEquals(Dictionary<string, int> left, Dictionary<string, int> right)
    {
        if (left.Count != right.Count)
            return false;
        foreach (var pair in left)
            if (!right.TryGetValue(pair.Key, out var value) || value != pair.Value)
                return false;
        return true;
    }
}
