using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaaFramework.Binding;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using MFAAvalonia.Helper.ValueType;
using MFAAvalonia.Models;
using MFAAvalonia.Services;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.MessageBox;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
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
    private List<WarehouseResourceSnapshot> _pendingResourceHistory = [];
    private bool _hasPendingChartChanges;
    private readonly System.Collections.Generic.Dictionary<string, int> _savedCore = new(StringComparer.Ordinal);
    private readonly System.Collections.Generic.Dictionary<string, int> _savedOther = new(StringComparer.Ordinal);
    private static readonly string DraftPath = Path.Combine(AppPaths.ConfigDirectory, "warehouse_scan.json");

    public ObservableCollection<WarehouseCoreResourceViewModel> CoreResources { get; } = [];
    public ObservableCollection<WarehouseCoreResourceViewModel> CoreIconResources { get; } = [];
    public ObservableCollection<WarehouseCoreResourceViewModel> CoreTextResources { get; } = [];
    public ObservableCollection<WarehouseOtherItemViewModel> OtherItems { get; } = [];
    public ObservableCollection<WarehouseChartViewModel> Charts { get; } = [];
    public bool HasOtherItems => OtherItems.Count > 0;
    public bool NoOtherItems => !HasOtherItems;
    public bool HasChartHistory => Charts.Any(chart => chart.HasHistory);
    public bool NoChartHistory => !HasChartHistory;
    public bool HasUnsavedChanges => _pendingResourceHistory.Count > 0
        || _hasPendingChartChanges
        || CoreResources.Any(HasCoreChanged)
        || OtherItems.Any(HasOtherChanged);
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
        }

        var normalizedOtherItems = WarehouseScanDraftService.NormalizeOtherItems(_editor.Data.OtherItems);
        _editor.Data.OtherItems = normalizedOtherItems;
        foreach (var pair in normalizedOtherItems)
        {
            if (pair.Value <= 0)
                continue;
            OtherItems.Add(new WarehouseOtherItemViewModel(pair.Key, pair.Value, OnDataChanged));
            _savedOther[pair.Key] = pair.Value;
        }
        OtherItems.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasOtherItems));
            OnPropertyChanged(nameof(NoOtherItems));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        };
        RebuildCharts();
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
        _savedOther.Clear();
        foreach (var item in OtherItems)
        {
            if (!string.IsNullOrWhiteSpace(item.Name) && item.Count > 0)
            {
                data.OtherItems[item.Name] = item.Count;
                _savedOther[item.Name] = item.Count;
            }
        }
        data.ResourceHistory = [.. _editor.Data.ResourceHistory, .. _pendingResourceHistory.Select(CloneSnapshot)];
        ConfigurationManager.Current.SetValue(ConfigurationKeys.WarehouseData, data);
        _editor.LoadData(data);
        _editor.Save();
        _pendingResourceHistory.Clear();
        _hasPendingChartChanges = false;
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

        var taskQueue = Instances.InstanceTabBarViewModel.ActiveTab?.TaskQueueViewModel;
        if (taskQueue == null)
        {
            ToastHelper.Warn("自动识别", "没有可用的游戏实例。");
            return;
        }

        IsRecognizing = true;
        try
        {
            WarehouseScanDraftService.Clear(DraftPath);
            if (taskQueue.Processor.MaaTasker is not { IsInitialized: true })
                taskQueue.Processor.TaskQueue.Enqueue(new MFATask
                {
                    Name = "仓库识别前启动",
                    Type = MFATask.MFATaskType.MFA,
                    Action = async () => await taskQueue.Processor.TestConnecting(),
                    OwnerViewModel = taskQueue,
                });

            taskQueue.Processor.TaskQueue.Enqueue(new MFATask
            {
                Name = "仓库自动识别",
                Type = MFATask.MFATaskType.MAAFW,
                OwnerViewModel = taskQueue,
                Action = async () =>
                {
                    try
                    {
                        var tasker = taskQueue.Processor.MaaTasker;
                        if (tasker is not { IsInitialized: true })
                            throw new InvalidOperationException("仓库识别前连接失败，请检查模拟器和游戏窗口。");
                        var job = tasker.AppendTask(new MaaNode { Name = "Warehouse_Start" });
                        if (job.WaitFor(MaaJobStatus.Succeeded) == null)
                            throw new InvalidOperationException("仓库自动识别任务执行失败。");
                        await DispatcherHelper.RunOnMainThreadAsync(LoadDraft);
                    }
                    finally
                    {
                        await DispatcherHelper.RunOnMainThreadAsync(() => IsRecognizing = false);
                    }
                },
            });
            taskQueue.Processor.Start(true, checkUpdate: false);
        }
        catch
        {
            IsRecognizing = false;
            throw;
        }
    }

    partial void OnIsRecognizingChanged(bool value) => OnPropertyChanged(nameof(IsIdle));
    private void OnDataChanged() => OnPropertyChanged(nameof(HasUnsavedChanges));

    private void LoadDraft()
    {
        var draft = WarehouseScanDraftService.Load(DraftPath);
        foreach (var item in CoreResources)
        {
            if (draft.CoreResources.TryGetValue(item.Name, out var value))
                item.Count = Math.Clamp(value, 0, item.Maximum);
        }

        OtherItems.Clear();
        foreach (var pair in WarehouseScanDraftService.NormalizeOtherItems(draft.OtherItems))
        {
            if (pair.Value <= 0)
                continue;
            OtherItems.Add(new WarehouseOtherItemViewModel(pair.Key, pair.Value, OnDataChanged));
        }

        _pendingResourceHistory = [.. draft.ResourceHistory.Select(CloneSnapshot)];
        OnPropertyChanged(nameof(HasOtherItems));
        OnPropertyChanged(nameof(NoOtherItems));
        OnPropertyChanged(nameof(HasChartHistory));
        OnPropertyChanged(nameof(NoChartHistory));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        ToastHelper.Success("仓库识别", "识别完成，结果已填入页面；点击“保存”后才会写入正式数据。");
    }
    private bool HasCoreChanged(WarehouseCoreResourceViewModel item) => item.Count != GetValue(_savedCore, item.Name);
    private bool HasOtherChanged(WarehouseOtherItemViewModel item) => item.Count != GetValue(_savedOther, item.Name);

    [RelayCommand]
    private void RefreshCharts()
    {
        RebuildCharts();
    }

    [RelayCommand]
    private void AddOtherItem()
    {
        OtherItems.Add(new WarehouseOtherItemViewModel(string.Empty, 0, OnDataChanged));
        OnPropertyChanged(nameof(HasOtherItems));
        OnPropertyChanged(nameof(NoOtherItems));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>移动其他物品的显示顺序。</summary>
    public void MoveOtherItem(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= OtherItems.Count || targetIndex >= OtherItems.Count || sourceIndex == targetIndex)
            return;

        OtherItems.Move(sourceIndex, targetIndex);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    [RelayCommand]
    private async Task ClearChartHistory()
    {
        if (_editor.Data.ResourceHistory.Count == 0)
            return;

        var result = await SukiMessageBox.ShowDialog(new SukiMessageBoxHost
        {
            Content = "确定要清空所有核心资源变化记录吗？",
            ActionButtonsPreset = SukiMessageBoxButtons.YesNo,
            IconPreset = SukiMessageBoxIcons.Warning,
        }, new SukiMessageBoxOptions { Title = "清空图表数据" });
        if (!result.Equals(SukiMessageBoxResult.Yes))
            return;

        _editor.Data.ResourceHistory.Clear();
        _hasPendingChartChanges = true;
        RebuildCharts();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    private void RebuildCharts()
    {
        Charts.Clear();
        foreach (var definition in CoreDefinitions)
            Charts.Add(new WarehouseChartViewModel(definition.Name, _editor.Data.ResourceHistory, DeleteChartPoint));
        OnPropertyChanged(nameof(HasChartHistory));
        OnPropertyChanged(nameof(NoChartHistory));
    }

    private static WarehouseResourceSnapshot CloneSnapshot(WarehouseResourceSnapshot snapshot) => new()
    {
        RecordedAt = snapshot.RecordedAt,
        Values = new Dictionary<string, int>(snapshot.Values, StringComparer.Ordinal),
    };

    private void DeleteChartPoint(string resourceName, int historyIndex)
    {
        if (historyIndex < 0 || historyIndex >= _editor.Data.ResourceHistory.Count)
            return;

        var snapshot = _editor.Data.ResourceHistory[historyIndex];
        if (!snapshot.Values.Remove(resourceName))
            return;

        if (snapshot.Values.Count == 0)
            _editor.Data.ResourceHistory.RemoveAt(historyIndex);

        _hasPendingChartChanges = true;
        RebuildCharts();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

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
    public bool IsVisible => Count > 0 || string.IsNullOrWhiteSpace(Name);
    partial void OnNameChanged(string value) => _changed();
    partial void OnCountChanged(int value)
    {
        OnPropertyChanged(nameof(IsVisible));
        _changed();
    }
}

public sealed class WarehouseChartViewModel
{
    public const double ChartWidth = 650;
    public const double ChartHeight = 150;
    public ObservableCollection<WarehouseChartPointViewModel> Points { get; } = [];

    public WarehouseChartViewModel(string name, IEnumerable<WarehouseResourceSnapshot> history, Action<string, int> deletePoint)
    {
        Name = name;
        var snapshots = history
            .Select((snapshot, index) => (Snapshot: snapshot, Index: index))
            .Where(item => item.Snapshot.Values.ContainsKey(name))
            .ToList();
        var values = snapshots
            .Select(item => item.Snapshot.Values[name])
            .ToList();
        PointCount = values.Count;
        if (values.Count > 0)
        {
            MinimumValue = values.Min();
            MaximumValue = values.Max();
            LatestValue = values[^1];
        }
        BuildPoints(name, snapshots, values, deletePoint);
    }
    public string Name { get; }
    public int PointCount { get; }
    public bool HasHistory => PointCount > 0;
    public string EmptyText => PointCount == 0 ? "暂无识别记录" : $"已有 {PointCount} 个记录点";
    public int MinimumValue { get; }
    public int MaximumValue { get; }
    public int LatestValue { get; }
    public string SummaryText => PointCount == 0
        ? "暂无识别记录"
        : $"当前 {LatestValue:N0} · 范围 {MinimumValue:N0}–{MaximumValue:N0}";
    public Geometry? LineGeometry { get; private set; }

    private void BuildPoints(string name, IReadOnlyList<(WarehouseResourceSnapshot Snapshot, int Index)> snapshots, IReadOnlyList<int> values, Action<string, int> deletePoint)
    {
        if (values.Count == 0)
            return;

        var minimum = values.Min();
        var maximum = values.Max();
        var span = Math.Max(1d, maximum - minimum);
        const double horizontalPadding = 18;
        var width = Math.Max(1d, ChartWidth - horizontalPadding * 2);
        var height = ChartHeight - 20;
        var firstRecordedAt = snapshots[0].Snapshot.RecordedAt;
        var lastRecordedAt = snapshots[^1].Snapshot.RecordedAt;
        var totalElapsedTicks = (lastRecordedAt - firstRecordedAt).Ticks;
        for (var i = 0; i < values.Count; i++)
        {
            var x = values.Count == 1
                ? ChartWidth / 2
                : totalElapsedTicks > 0
                    ? horizontalPadding + width * (snapshots[i].Snapshot.RecordedAt - firstRecordedAt).Ticks / totalElapsedTicks
                    : horizontalPadding + width * i / (values.Count - 1);
            var y = 10 + height - (values[i] - minimum) / span * height;
            Points.Add(new WarehouseChartPointViewModel(x, y, values[i], snapshots[i].Snapshot.RecordedAt, snapshots[i].Index, name, deletePoint));
        }

        if (Points.Count < 2)
            return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(Points[0].X, Points[0].Y), false);
            foreach (var point in Points.Skip(1))
                context.LineTo(new Point(point.X, point.Y));
        }
        LineGeometry = geometry;
    }
}

public sealed class WarehouseChartPointViewModel
{
    public WarehouseChartPointViewModel(double x, double y, int value, DateTime recordedAt, int historyIndex, string resourceName, Action<string, int> deletePoint)
    {
        X = x;
        Y = y;
        Value = value;
        RecordedAt = recordedAt;
        DeleteCommand = new RelayCommand(() => deletePoint(resourceName, historyIndex));
    }

    public double X { get; }
    public double Y { get; }
    public double Left => X - 6;
    public double Top => Y - 6;
    public int Value { get; }
    public DateTime RecordedAt { get; }
    public string TooltipText => $"{RecordedAt:yyyy-MM-dd HH:mm:ss}\n{Value:N0}";
    public ICommand DeleteCommand { get; }
}
