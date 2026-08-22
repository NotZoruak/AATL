using System.Collections.Generic;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 修刀筛选条件。筛选条件通过 pipeline 参数中的布尔字段传递。
/// </summary>
public sealed class RepairFilterSelection
{
    private RepairFilterSelection(HashSet<string> swordTypes, HashSet<string> damageStates)
    {
        SwordTypes = swordTypes;
        DamageStates = damageStates;
    }

    /// <summary>已选择的刀剑种类。</summary>
    public HashSet<string> SwordTypes { get; }

    /// <summary>已选择的伤势状况。</summary>
    public HashSet<string> DamageStates { get; }

    /// <summary>是否至少选择了一项筛选条件。</summary>
    public bool HasAnyFilter => SwordTypes.Count > 0 || DamageStates.Count > 0;

    /// <summary>判断固定筛选标题区域的 OCR 文本是否可接受。</summary>
    public static bool IsFilterTitle(string? text)
    {
        return text?.Contains('选') == true;
    }

    /// <summary>从界面注入的布尔参数构造筛选条件。</summary>
    public static RepairFilterSelection FromFlags(IReadOnlyDictionary<string, bool> flags)
    {
        var swordTypes = new HashSet<string>();
        var damageStates = new HashSet<string>();

        foreach (var pair in flags)
        {
            if (!pair.Value) continue;

            if (pair.Key.StartsWith("sword_type_", System.StringComparison.Ordinal))
                swordTypes.Add(pair.Key["sword_type_".Length..]);
            else if (pair.Key.StartsWith("damage_", System.StringComparison.Ordinal))
                damageStates.Add(pair.Key["damage_".Length..]);
        }

        return new RepairFilterSelection(swordTypes, damageStates);
    }
}
