using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.Configuration;
using MFAAvalonia.Helper;
using MFAAvalonia.Models;
using MFAAvalonia.Services;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.MessageBox;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MFAAvalonia.ViewModels.Pages;

/// <summary>仓库页面，展示核心资源与其他物品。</summary>
public partial class WarehouseViewModel : ViewModelBase
{
    private static readonly (string Key, string Name, string Icon, int Maximum)[] CoreDefinitions =
    [
        ("木炭", "木炭", "木炭.jpg", 9999999),
        ("玉钢", "玉钢", "玉鋼.jpg", 9999999),
        ("冷却材", "冷却材", "冷却材.jpg", 9999999),
        ("砥石", "砥石", "砥石.jpg", 9999999),
        ("委托符", "委托符", "", 9999),
        ("加速符", "加速符", "", 9999),
        ("小判", "小判", "", 99999999),
    ];

    private readonly WarehouseDataEditor _editor;
    private readonly System.Collections.Generic.Dictionary<string, int> _savedCore = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, int> _savedOther = new(StringComparer.Ordinal);

    public ObservableCollection<WarehouseCoreResourceViewModel> CoreResources { get; } = [];
    public ObservableCollection<WarehouseCoreResourceViewModel> CoreIconResources { get; } = [];
    public ObservableCollection<WarehouseCoreResourceViewModel> CoreTextResources { get; } = [];
    public ObservableCollection<WarehouseOtherItemViewModel> OtherItems { get; } = [];
    public ObservableCollection<WarehouseChartViewModel> Charts { get; } = [];
    public bool HasOtherItems => OtherItems.Count > 0;
    public bool NoOtherItems => !HasOtherItems;
    public bool HasChartHistory => Charts.Any(chart => chart.HasHistory);
    public bool NoChartHistory => !HasChartHistory;
    public bool HasUnsavedChanges => CoreResources.Any(HasCoreChanged) || OtherItems.Any(HasOtherChanged);
    public bool IsIdle => !IsRecognizing;

    [ObservableProperty] private bool _isRecognizing;

    public WarehouseViewModel()
    {
        _editor = new WarehouseDataEditor(ConfigurationManager.Current.GetValue(ConfigurationKeys.WarehouseData, new WarehouseData()));
        foreach (var definition in CoreDefinitions)
        {
            var value = GetValue(_editor.Data.CoreResources, definition.Key);
            var resource = new WarehouseCoreResourceViewModel(definition.Key, definition.Name, LoadIcon(definition.Icon), definition.Maximum, value, OnDataChanged);
            CoreResources.Add(resource);
            if (resource.HasIcon)
                CoreIconResources.Add(resource);
            else
                CoreTextResources.Add(resource);
            _savedCore[definition.Key] = value;
            Charts.Add(new WarehouseChartViewModel(definition.Name, _editor.Data.ResourceHistory.Count));
        }

        foreach (var pair in _editor.Data.OtherItems.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            OtherItems.Add(new WarehouseOtherItemViewModel(pair.Key, pair.Value, OnDataChanged));
            _savedOther[pair.Key] = pair.Value;
        }
    }

    [RelayCommand]
    private void Save()
    {
        var data = new WarehouseData();
        foreach (var item in CoreResources)
        {
            data.CoreResources[item.Name] = item.Count;
            _savedCore[item.Name] = item.Count;
        }
        foreach (var item in OtherItems)
        {
            if (!string.IsNullOrWhiteSpace(item.Name))
                data.OtherItems[item.Name] = item.Count;
            _savedOther[item.Name] = item.Count;
        }
        data.ResourceHistory = [.. _editor.Data.ResourceHistory];
        ConfigurationManager.Current.SetValue(ConfigurationKeys.WarehouseData, data);
        _editor.Save();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private void Revert()
    {
        foreach (var item in CoreResources)
            item.Count = GetValue(_savedCore, item.Name);
        foreach (var item in OtherItems)
            item.Count = GetValue(_savedOther, item.Name);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private async Task Clear()
    {
        var result = await SukiMessageBox.ShowDialog(new SukiMessageBoxHost
        {
            Content = "确定要清空仓库中的所有识别数据吗？",
            ActionButtonsPreset = SukiMessageBoxButtons.YesNo,
            IconPreset = SukiMessageBoxIcons.Warning,
        }, new SukiMessageBoxOptions { Title = "清空仓库数据" });
        if (!result.Equals(SukiMessageBoxResult.Yes))
            return;

        foreach (var item in CoreResources)
            item.Count = 0;
        foreach (var item in OtherItems)
            item.Count = 0;
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private void AutoRecognize()
    {
        if (IsRecognizing)
            return;
        IsRecognizing = true;
        ToastHelper.Info("仓库识别", "自动识别流程将在下一步接入，当前可以先编辑和保存仓库数据。");
        IsRecognizing = false;
    }

    partial void OnIsRecognizingChanged(bool value) => OnPropertyChanged(nameof(IsIdle));
    private void OnDataChanged() => OnPropertyChanged(nameof(HasUnsavedChanges));
    private bool HasCoreChanged(WarehouseCoreResourceViewModel item) => item.Count != GetValue(_savedCore, item.Name);
    private bool HasOtherChanged(WarehouseOtherItemViewModel item) => item.Count != GetValue(_savedOther, item.Name);

    private static int GetValue(System.Collections.Generic.Dictionary<string, int> values, string key) =>
        values.TryGetValue(key, out var value) ? value : 0;

    private static Bitmap? LoadIcon(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        var path = Path.Combine(AppPaths.ResourceDirectory, "base", "image", "resource-icon", fileName);
        return File.Exists(path) ? new Bitmap(path) : null;
    }
}

public sealed partial class WarehouseCoreResourceViewModel : ObservableObject
{
    private readonly Action _changed;
    public WarehouseCoreResourceViewModel(string name, string displayName, Bitmap? icon, int maximum, int count, Action changed)
    {
        Name = name; DisplayName = displayName; Icon = icon; Maximum = maximum; _count = count; _changed = changed;
    }
    public string Name { get; }
    public string DisplayName { get; }
    public Bitmap? Icon { get; }
    public int Maximum { get; }
    public bool HasIcon => Icon != null;
    public bool HasNoIcon => Icon == null;
    [ObservableProperty] private int _count;
    partial void OnCountChanged(int value) => _changed();
}

public sealed partial class WarehouseOtherItemViewModel : ObservableObject
{
    private readonly Action _changed;
    public WarehouseOtherItemViewModel(string name, int count, Action changed)
    {
        _name = name; _count = count; _changed = changed;
    }
    [ObservableProperty] private string _name;
    [ObservableProperty] private int _count;
    partial void OnNameChanged(string value) => _changed();
    partial void OnCountChanged(int value) => _changed();
}

public sealed class WarehouseChartViewModel
{
    public WarehouseChartViewModel(string name, int pointCount)
    {
        Name = name; PointCount = pointCount;
    }
    public string Name { get; }
    public int PointCount { get; }
    public bool HasHistory => PointCount > 0;
    public string EmptyText => PointCount == 0 ? "暂无识别记录" : $"已有 {PointCount} 个记录点";
}
