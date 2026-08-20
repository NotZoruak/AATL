using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.Helper;
using MFAAvalonia.Models;
using MFAAvalonia.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace MFAAvalonia.ViewModels.Pages;

/// <summary>工作记录页：解析日志并展示运行记录</summary>
public partial class WorkRecordsViewModel : ViewModelBase
{
    /// <summary>运行记录列表（时间倒序）</summary>
    [ObservableProperty] private ObservableCollection<WorkRecord> _records = [];

    /// <summary>当前选中记录</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDurationText))]
    [NotifyPropertyChangedFor(nameof(SelectedResourcesText))]
    [NotifyPropertyChangedFor(nameof(SelectedHasResources))]
    [NotifyPropertyChangedFor(nameof(SelectedSwordDropsText))]
    [NotifyPropertyChangedFor(nameof(SelectedHasSwordDrops))]
    [NotifyPropertyChangedFor(nameof(SelectedSwordDropGroups))]
    [NotifyPropertyChangedFor(nameof(SelectedHasOutcomes))]
    [NotifyPropertyChangedFor(nameof(SelectedLogisticsText))]
    [NotifyPropertyChangedFor(nameof(SelectedHasLogistics))]
    [NotifyPropertyChangedFor(nameof(SelectedSpecialEventsText))]
    [NotifyPropertyChangedFor(nameof(SelectedHasSpecialEvents))]
    [NotifyPropertyChangedFor(nameof(SelectedHasEarlyEnd))]
    private WorkRecord? _selectedRecord;

    /// <summary>刀种展示顺序</summary>
    private static readonly string[] TypeOrder = WorkRecordBuilder.SwordTypeOrder;

    /// <summary>刷新：重新扫描全部日志并重建列表</summary>
    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var entries = new List<LogEntry>();
            var dir = AppPaths.LogsDirectory;
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "log-*.log").OrderBy(f => f))
                    entries.AddRange(LogParser.ParseFile(file));
            }

            var records = WorkRecordBuilder.Build(entries);
            // 时间倒序
            Records = new ObservableCollection<WorkRecord>(
                records.OrderByDescending(r => r.StartTime).ToList());
            SelectedRecord = Records.FirstOrDefault();
        }
        catch (Exception ex)
        {
            // 解析失败不影响页面可用性：记录错误并保留现有列表
            LoggerHelper.Error($"工作记录刷新失败：{ex.Message}");
        }
    }

    // ---------- 选中记录卡片展示字段 ----------

    /// <summary>是否有提前结束（0 次不显示该行）</summary>
    public bool SelectedHasEarlyEnd => SelectedRecord?.EarlyEndCount > 0;

    /// <summary>耗时文本：1 小时 27 分 / 35 分钟</summary>
    public string SelectedDurationText => SelectedRecord?.DurationText ?? "";

    /// <summary>资源收获文本：木炭x240 玉钢x60 小判箱x3</summary>
    public string SelectedResourcesText =>
        SelectedRecord is null
            ? ""
            : string.Join("  ", SelectedRecord.ResourceGains
                .OrderBy(kv => kv.Key == "小判箱" ? 1 : 0) // 小判箱放最后
                .Select(kv => $"{kv.Key}x{kv.Value}"));

    /// <summary>是否有资源收获</summary>
    public bool SelectedHasResources => SelectedRecord?.ResourceGains.Count > 0;

    /// <summary>刀剑掉落分组文本（短胁打太大太枪薙剑序，每组一行）</summary>
    public string SelectedSwordDropsText
    {
        get
        {
            if (SelectedRecord is null)
                return "";
            var sb = new StringBuilder();
            foreach (var group in SelectedRecord.SwordDrops
                         .GroupBy(d => d.SwordType)
                         .OrderBy(g => Array.IndexOf(TypeOrder, g.Key) is var i && i < 0 ? int.MaxValue : i))
            {
                sb.AppendLine($"{group.Key}:{string.Join("，", group
                    .GroupBy(d => d.SwordName)
                    .Select(g => $"{g.Key}（x{g.Count()}）"))}");
            }
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>刀剑掉落分组展示数据</summary>
    public IReadOnlyList<SwordDropGroupDisplay> SelectedSwordDropGroups
    {
        get
        {
            if (SelectedRecord is null)
                return [];

            return SelectedRecord.SwordDrops
                .GroupBy(d => d.SwordType)
                .OrderBy(g => Array.IndexOf(TypeOrder, g.Key) is var i && i < 0 ? int.MaxValue : i)
                .Select(g => new SwordDropGroupDisplay(
                    g.Key,
                    string.Join("，", g.GroupBy(d => d.SwordName)
                        .Select(nameGroup => $"{nameGroup.Key}（x{nameGroup.Count()}）"))))
                .ToList();
        }
    }

    /// <summary>是否有刀剑掉落</summary>
    public bool SelectedHasSwordDrops => SelectedRecord?.SwordDrops.Count > 0;

    /// <summary>是否有出阵收获</summary>
    public bool SelectedHasOutcomes => SelectedHasResources || SelectedHasSwordDrops;

    /// <summary>后勤记录文本</summary>
    public string SelectedLogisticsText
    {
        get
        {
            if (SelectedRecord is null)
                return "";
            var sb = new StringBuilder();
            foreach (var kv in SelectedRecord.LogisticsCounts.OrderByDescending(kv => kv.Value))
            {
                sb.AppendLine($"{kv.Key} ×{kv.Value}");
                if (kv.Key == "派遣远征")
                    foreach (var d in SelectedRecord.LogisticsDispatches)
                        sb.AppendLine($"  {d.Time:HH:mm}  {d.Unit} → {d.Map}");
            }
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>是否有后勤记录</summary>
    public bool SelectedHasLogistics => SelectedRecord?.LogisticsCounts.Count > 0;

    /// <summary>特殊情况文本</summary>
    public string SelectedSpecialEventsText =>
        SelectedRecord is null
            ? ""
            : string.Join("\n", SelectedRecord.SpecialEvents.Select(e => $"{e.Time:MM-dd HH:mm} {e.Description}"));

    /// <summary>是否有特殊情况</summary>
    public bool SelectedHasSpecialEvents => SelectedRecord?.SpecialEvents.Count > 0;
}

/// <summary>刀剑掉落按刀种分组后的展示数据</summary>
public sealed record SwordDropGroupDisplay(string SwordType, string DropsText);
