using System.Collections.Generic;

namespace MFAAvalonia.Models;

/// <summary>编队预设：一份完整的部队配置</summary>
public class FormationPreset
{
    /// <summary>预设唯一标识（新增时递增分配，复制/删除不影响其他预设的引用）</summary>
    public int Id { get; set; }

    /// <summary>预设名称（兼作备注用途，如「7-4练级队」）</summary>
    public string Name { get; set; } = "";

    /// <summary>目标部队编号，1-5</summary>
    public int Team { get; set; } = 1;

    /// <summary>1-6 号位配置（第 1 位为队长），长度固定为 6</summary>
    public List<FormationSlot> Slots { get; set; } = [];

    /// <summary>补齐 Slots 至 6 个位置，缺位补空槽</summary>
    public void EnsureSlots()
    {
        Slots ??= [];
        while (Slots.Count < 6)
            Slots.Add(new FormationSlot());
        if (Slots.Count > 6)
            Slots = Slots.GetRange(0, 6);
    }
}

/// <summary>编队预设中单个位置的配置</summary>
public class FormationSlot
{
    /// <summary>刀剑男士名称，空表示不指定</summary>
    public string Sword { get; set; } = "";

    /// <summary>刀装名称，空表示不指定</summary>
    public string Equip { get; set; } = "";

    /// <summary>马匹名称，「无」表示不装备</summary>
    public string Horse { get; set; } = "无";
}
