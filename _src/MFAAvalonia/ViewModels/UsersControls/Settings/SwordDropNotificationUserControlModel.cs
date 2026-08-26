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

/// <summary>刀剑掉落播报名单，使用刀剑名册搜索并保存基础名称。</summary>
public partial class SwordDropNotificationUserControlModel : ViewModelBase
{
    private static readonly string[] TypeOrder = ["短刀", "胁差", "打刀", "太刀", "大太刀", "枪", "薙刀", "剑"];
    private sealed record CatalogEntry(string BaseName, string Type, string DisplayName);
    private sealed record CatalogRawItem(string Number, string Type, string Name, bool TypeOnly = false);

    private readonly List<CatalogEntry> _catalog = [];
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);

    public ObservableCollection<SwordDropNotificationTagItem> Tags { get; } = [];
    public ObservableCollection<SwordDropNotificationCandidateItem> Candidates { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;

    public SwordDropNotificationUserControlModel()
    {
        LoadCatalog();
        LoadList();
    }

    private void LoadCatalog()
    {
        var path = Path.Combine(AppPaths.ResourceDirectory, "base", "SwordBookCatalog.json");
        if (!File.Exists(path))
            return;

        var items = (JsonConvert.DeserializeObject<List<CatalogRawItem>>(File.ReadAllText(path)) ?? [])
            .Where(item => !item.TypeOnly).ToList();
        var duplicateNames = items.GroupBy(item => item.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => group.Last().Number, StringComparer.Ordinal);
        foreach (var item in items)
        {
            var displayName = duplicateNames.TryGetValue(item.Name, out var lastNumber) && lastNumber == item.Number
                ? $"{item.Name}·极" : item.Name;
            var baseName = displayName.EndsWith("·极", StringComparison.Ordinal) ? displayName[..^2] : displayName;
            if (_catalog.All(entry => entry.BaseName != baseName))
                _catalog.Add(new CatalogEntry(baseName, item.Type, displayName));
        }
    }

    private void LoadList()
    {
        if (ConfigurationManager.Current.TryGetValue(
                ConfigurationKeys.SwordDropNotificationSwords, out List<string>? saved))
        {
            foreach (var name in saved ?? [])
            {
                if (_catalog.Any(entry => entry.BaseName == name))
                    _selected.Add(name);
            }
        }

        RefreshTags();
    }

    partial void OnSearchTextChanged(string value) => RefreshCandidates();

    private static int TypeRank(string type)
    {
        var index = Array.IndexOf(TypeOrder, type);
        return index < 0 ? TypeOrder.Length : index;
    }

    private IEnumerable<CatalogEntry> SortedSelected() => _catalog
        .Where(entry => _selected.Contains(entry.BaseName))
        .OrderBy(entry => TypeRank(entry.Type))
        .ThenBy(entry => _catalog.IndexOf(entry));

    private void RefreshTags()
    {
        Tags.Clear();
        foreach (var entry in SortedSelected())
            Tags.Add(new SwordDropNotificationTagItem(entry.DisplayName, entry.Type, entry.BaseName, RemoveCommand));
    }

    private void RefreshCandidates()
    {
        Candidates.Clear();
        var keyword = SearchText?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
            return;

        foreach (var entry in _catalog.Where(entry => entry.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(entry => TypeRank(entry.Type)).ThenBy(entry => _catalog.IndexOf(entry)))
        {
            Candidates.Add(new SwordDropNotificationCandidateItem(entry.DisplayName, entry.Type,
                entry.BaseName, _selected.Contains(entry.BaseName), AddCommand));
        }
    }

    [RelayCommand]
    private void Add(string baseName)
    {
        if (!_selected.Add(baseName)) return;
        Persist();
        RefreshTags();
        RefreshCandidates();
    }

    [RelayCommand]
    private void Remove(string baseName)
    {
        if (!_selected.Remove(baseName)) return;
        Persist();
        RefreshTags();
        RefreshCandidates();
    }

    private void Persist() => ConfigurationManager.Current.SetValue(
        ConfigurationKeys.SwordDropNotificationSwords, SortedSelected().Select(entry => entry.BaseName).ToList());
}

public partial class SwordDropNotificationTagItem : ObservableObject
{
    private readonly IRelayCommand<string> _removeCommand;
    public SwordDropNotificationTagItem(string displayName, string type, string baseName, IRelayCommand<string> removeCommand)
    { DisplayName = displayName; Type = type; BaseName = baseName; _removeCommand = removeCommand; }
    public string DisplayName { get; }
    public string Type { get; }
    public string BaseName { get; }
    [RelayCommand] private void Remove() => _removeCommand.Execute(BaseName);
}

public partial class SwordDropNotificationCandidateItem : ObservableObject
{
    private readonly IRelayCommand<string> _addCommand;
    public SwordDropNotificationCandidateItem(string displayName, string type, string baseName, bool isSelected, IRelayCommand<string> addCommand)
    { DisplayName = displayName; Type = type; BaseName = baseName; IsSelected = isSelected; _addCommand = addCommand; }
    public string DisplayName { get; }
    public string Type { get; }
    public string BaseName { get; }
    [ObservableProperty] private bool _isSelected;
    [RelayCommand] private void Add() => _addCommand.Execute(BaseName);
}
