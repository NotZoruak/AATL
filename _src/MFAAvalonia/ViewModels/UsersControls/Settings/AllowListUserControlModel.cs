using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.Configuration;
using MFAAvalonia.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace MFAAvalonia.ViewModels.UsersControls.Settings;

/// <summary>
/// 刀解/合成许可名单：供日课合成、锻刀前刀解等消耗刀剑功能共同使用的全局名单。
/// 名称只允许来自刀剑名册，上锁刀剑始终排除，普通与极化形态按名称共享许可状态。
/// </summary>
public partial class AllowListUserControlModel : ViewModelBase
{
    /// <summary>首次初始化写入的默认名单，之后不覆盖用户修改。</summary>
    private static readonly string[] DefaultSwords =
    [
        "加州清光", "歌仙兼定", "陆奥守吉行", "山姥切国广", "蜂须贺虎彻",
        "前田藤四郎", "秋田藤四郎", "乱藤四郎", "五虎退", "药研藤四郎", "爱染国俊", "小夜左文字",
        "笑面青江", "鲶尾藤四郎", "骨喰藤四郎", "堀川国广",
    ];

    /// <summary>标签按刀种排序的固定顺序（刀帐顺序）。</summary>
    private static readonly string[] TypeOrder = ["短刀", "胁差", "打刀", "太刀", "大太刀", "枪", "薙刀", "剑"];

    /// <summary>名册条目：BaseName 为基础名（不含「·极」），DisplayName 为显示名（极化条目带「·极」）。</summary>
    private sealed record CatalogEntry(string BaseName, string Type, string DisplayName);

    private sealed record CatalogRawItem(string Number, string Type, string Name, bool TypeOnly = false);

    private readonly List<CatalogEntry> _catalog = [];
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);

    /// <summary>已选标签列表（按刀种排序）。</summary>
    public ObservableCollection<AllowListTagItem> Tags { get; } = [];

    /// <summary>搜索候选列表（按刀种排序）。</summary>
    public ObservableCollection<AllowListCandidateItem> Candidates { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;

    public AllowListUserControlModel()
    {
        LoadCatalog();
        LoadAllowList();
    }

    /// <summary>从刀剑名册加载全部条目（过滤 typeOnly，重名条目的极化形态显示「·极」）。</summary>
    private void LoadCatalog()
    {
        var path = Path.Combine(AppPaths.ResourceDirectory, "base", "SwordBookCatalog.json");
        if (!File.Exists(path))
            return;

        var items = (JsonConvert.DeserializeObject<List<CatalogRawItem>>(File.ReadAllText(path)) ?? [])
            .Where(item => !item.TypeOnly)
            .ToList();
        var duplicateNames = items.GroupBy(item => item.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => group.Last().Number, StringComparer.Ordinal);
        foreach (var item in items)
        {
            var displayName = duplicateNames.TryGetValue(item.Name, out var lastNumber) && lastNumber == item.Number
                ? $"{item.Name}·极"
                : item.Name;
            var baseName = displayName.EndsWith("·极", StringComparison.Ordinal) ? displayName[..^2] : displayName;
            if (_catalog.All(entry => entry.BaseName != baseName))
                _catalog.Add(new CatalogEntry(baseName, item.Type, displayName));
        }
    }

    /// <summary>读取许可名单；首次初始化时写入默认名单。</summary>
    private void LoadAllowList()
    {
        if (!ConfigurationManager.Current.TryGetValue(ConfigurationKeys.AllowListSwords, out List<string>? saved))
        {
            saved = DefaultSwords.Where(name => _catalog.Any(entry => entry.BaseName == name)).ToList();
            ConfigurationManager.Current.SetValue(ConfigurationKeys.AllowListSwords, saved);
        }

        foreach (var name in saved ?? [])
        {
            if (_catalog.Any(entry => entry.BaseName == name))
                _selected.Add(name);
        }
        RefreshTags();
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshCandidates();
    }

    /// <summary>按刀种排序索引，未知名刀种排在末尾。</summary>
    private static int TypeRank(string type)
    {
        var index = Array.IndexOf(TypeOrder, type);
        return index < 0 ? TypeOrder.Length : index;
    }

    private IEnumerable<CatalogEntry> SortedSelected()
        => _catalog.Where(entry => _selected.Contains(entry.BaseName))
            .OrderBy(entry => TypeRank(entry.Type))
            .ThenBy(entry => _catalog.IndexOf(entry));

    private void RefreshTags()
    {
        Tags.Clear();
        foreach (var entry in SortedSelected())
            Tags.Add(new AllowListTagItem(entry.DisplayName, entry.Type, entry.BaseName, RemoveCommand));
    }

    private void RefreshCandidates()
    {
        Candidates.Clear();
        var keyword = SearchText?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
            return;

        foreach (var entry in _catalog
                     .Where(entry => entry.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(entry => TypeRank(entry.Type))
                     .ThenBy(entry => _catalog.IndexOf(entry)))
        {
            Candidates.Add(new AllowListCandidateItem(entry.DisplayName, entry.Type,
                entry.BaseName, _selected.Contains(entry.BaseName), AddCommand));
        }
    }

    [RelayCommand]
    private void Add(string baseName)
    {
        if (!_selected.Add(baseName))
            return;
        Persist();
        RefreshTags();
        RefreshCandidates();
    }

    [RelayCommand]
    private void Remove(string baseName)
    {
        if (!_selected.Remove(baseName))
            return;
        Persist();
        RefreshTags();
        RefreshCandidates();
    }

    private void Persist()
    {
        ConfigurationManager.Current.SetValue(ConfigurationKeys.AllowListSwords, SortedSelected().Select(entry => entry.BaseName).ToList());
    }
}

/// <summary>许可名单已选标签条目。</summary>
public partial class AllowListTagItem : ObservableObject
{
    private readonly IRelayCommand<string> _removeCommand;

    public AllowListTagItem(string displayName, string type, string baseName, IRelayCommand<string> removeCommand)
    {
        DisplayName = displayName;
        Type = type;
        BaseName = baseName;
        _removeCommand = removeCommand;
    }

    public string DisplayName { get; }
    public string Type { get; }
    public string BaseName { get; }

    [RelayCommand]
    private void Remove() => _removeCommand.Execute(BaseName);
}

/// <summary>许可名单搜索候选条目。</summary>
public partial class AllowListCandidateItem : ObservableObject
{
    private readonly IRelayCommand<string> _addCommand;

    public AllowListCandidateItem(string displayName, string type, string baseName, bool isSelected, IRelayCommand<string> addCommand)
    {
        DisplayName = displayName;
        Type = type;
        BaseName = baseName;
        IsSelected = isSelected;
        _addCommand = addCommand;
    }

    public string DisplayName { get; }
    public string Type { get; }
    public string BaseName { get; }

    [ObservableProperty] private bool _isSelected;

    [RelayCommand]
    private void Add() => _addCommand.Execute(BaseName);
}
