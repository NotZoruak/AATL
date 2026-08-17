using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.Helper;
using MFAAvalonia.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MFAAvalonia.ViewModels.UsersControls;

/// <summary>编辑编队预设：目标部队、1-6 号位（刀剑 / 刀装 / 马匹）。名称在任务设置页行内修改，不在此编辑</summary>
public partial class FormationEditorViewModel : ViewModelBase
{
    /// <summary>目标部队下拉选中索引（0-4 对应部队一至五）</summary>
    [ObservableProperty] private int _teamIndex;

    /// <summary>编成前卸下现有装备开关</summary>
    [ObservableProperty] private bool _clearEquipmentBeforeFormation;

    /// <summary>编成后保存游戏部队记录开关</summary>
    [ObservableProperty] private bool _saveGameFormationRecordAfterFormation;

    /// <summary>部队下拉选项（1-5）</summary>
    public string[] TeamOptions { get; } = ["部队一", "部队二", "部队三", "部队四", "部队五"];

    /// <summary>1-6 号位编辑行</summary>
    public ObservableCollection<FormationSlotEdit> Slots { get; } = [];

    /// <summary>从剪影模板文件名读取的刀剑名称，用于可搜索下拉框</summary>
    public string[] SwordOptions { get; } = FormationOptions.LoadSwordOptions();

    private readonly FormationPreset _preset;

    /// <summary>保存回调：preset 参数非 null 表示保存，null 表示取消关闭</summary>
    private readonly Action<FormationPreset?>? _onDone;

    public FormationEditorViewModel(FormationPreset preset, Action<FormationPreset?>? onDone)
    {
        _preset = preset;
        _onDone = onDone;
        _teamIndex = Math.Clamp(preset.Team - 1, 0, 4);
        _clearEquipmentBeforeFormation = preset.ClearEquipmentBeforeFormation;
        _saveGameFormationRecordAfterFormation = preset.SaveGameFormationRecordAfterFormation;
        preset.EnsureSlots();
        for (var i = 0; i < 6; i++)
        {
            Slots.Add(new FormationSlotEdit(i + 1, preset.Slots[i]));
        }
    }

    [RelayCommand]
    private void Save()
    {
        // 名称不在此编辑，保持原值
        _preset.Team = TeamIndex + 1;
        _preset.ClearEquipmentBeforeFormation = ClearEquipmentBeforeFormation;
        _preset.SaveGameFormationRecordAfterFormation = SaveGameFormationRecordAfterFormation;
        _preset.EnsureSlots();
        for (var i = 0; i < Slots.Count && i < 6; i++)
        {
            _preset.Slots[i].Sword = Slots[i].Sword.Trim();
            _preset.Slots[i].Equip = Slots[i].Equip;
            _preset.Slots[i].Horse = string.IsNullOrEmpty(Slots[i].Horse) ? "无" : Slots[i].Horse;
        }
        _onDone?.Invoke(_preset);
    }

    [RelayCommand]
    private void Cancel()
    {
        _onDone?.Invoke(null);
    }
}

/// <summary>预设编辑界面中的单个位置行（可绑定）</summary>
public partial class FormationSlotEdit : ObservableObject
{
    /// <summary>位置编号 1-6</summary>
    public int Position { get; }

    [ObservableProperty] private string _sword;
    [ObservableProperty] private string _equip;
    [ObservableProperty] private string _horse;

    /// <summary>马匹下拉选项（含「无」）</summary>
    public string[] HorseOptions => FormationOptions.HorseOptions;

    public FormationSlotEdit(int position, FormationSlot slot)
    {
        Position = position;
        _sword = slot.Sword;
        _equip = slot.Equip;
        _horse = slot.Horse;
    }
}

/// <summary>编队编辑器使用的候选列表</summary>
public static class FormationOptions
{
    public static readonly string[] HorseOptions = ["无", "三河栗毛", "小云雀", "松风", "彦星"];

    /// <summary>从已有剪影模板中提取并排序全部刀剑名称</summary>
    public static string[] LoadSwordOptions()
    {
        var directory = Path.Combine(AppPaths.ResourceDirectory, "silhouette");
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory, "*_head.png")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => Regex.Match(name ?? string.Empty, @"^\d+_(.+)_head$"))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.CurrentCulture)
            .ToArray();
    }
}
