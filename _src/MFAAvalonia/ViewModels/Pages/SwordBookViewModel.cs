using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaaFramework.Binding;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using MFAAvalonia.Helper.ValueType;
using MFAAvalonia.Models;
using MFAAvalonia.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SukiUI.Controls;
using SukiUI.Dialogs;
using SukiUI.MessageBox;

namespace MFAAvalonia.ViewModels.Pages;

/// <summary>刀帐页面，管理刀剑条目和四类立绘拥有状态。</summary>
public partial class SwordBookViewModel : ViewModelBase
{
    private readonly Dictionary<string, SwordBookEntry> _savedEntries = new(StringComparer.Ordinal);
    private static readonly string DraftPath = Path.Combine(AppPaths.ConfigDirectory, "swordbook_scan.json");

    public ObservableCollection<SwordBookRowViewModel> Entries { get; } = [];
    public string Instruction => "请先将游戏页面切换至序号最小的已拥有刀剑男士的刀帐页面，自动识别将从当前刀剑开始扫描。";
    public bool HasUnsavedChanges => Entries.Any(HasChanged);
    public bool IsIdle => !IsRecognizing;

    [ObservableProperty] private bool _isRecognizing;

    public SwordBookViewModel()
    {
        LoadCatalog();
        LoadSavedState();
        UpdateDataPersistenceService.SwordBookDataSaved += OnSwordBookDataSaved;
    }

    [RelayCommand(CanExecute = nameof(HasUnsavedChanges))]
    private void Save()
    {
        var values = Entries.ToDictionary(row => row.Number, ToEntry, StringComparer.Ordinal);
        _savedEntries.Clear();
        foreach (var pair in values)
            _savedEntries[pair.Key] = pair.Value.Clone();
        ConfigurationManager.Current.SetValue(ConfigurationKeys.SwordBookEntries, values.Values.Select(ToState).ToList());
        foreach (var row in Entries)
            row.MarkSaved();
        NotifySavedStateChanged();
    }

    [RelayCommand(CanExecute = nameof(HasUnsavedChanges))]
    private void Revert()
    {
        foreach (var row in Entries)
            if (_savedEntries.TryGetValue(row.Number, out var entry))
                row.Apply(entry);
        NotifySavedStateChanged();
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
            if (taskQueue.Processor.MaaTasker is not { IsInitialized: true })
                taskQueue.Processor.TaskQueue.Enqueue(new MFATask
                {
                    Name = "刀帐识别前启动",
                    Type = MFATask.MFATaskType.MFA,
                    Action = async () => await taskQueue.Processor.TestConnecting(),
                    OwnerViewModel = taskQueue,
                });

            taskQueue.Processor.TaskQueue.Enqueue(new MFATask
            {
                Name = "刀帐自动识别",
                Type = MFATask.MFATaskType.MAAFW,
                OwnerViewModel = taskQueue,
                Action = async () =>
                {
                    try
                    {
                        var tasker = taskQueue.Processor.MaaTasker;
                        if (tasker is not { IsInitialized: true })
                            throw new InvalidOperationException("刀帐识别前连接失败，请检查模拟器和游戏窗口。");
                        var job = tasker.AppendTask(new MaaNode { Name = "SwordBookScan" });
                        if (job.WaitFor(MaaJobStatus.Succeeded) == null)
                        {
                            await ShowRecognitionFailureAsync();
                            return;
                        }
                        await DispatcherHelper.RunOnMainThreadAsync(LoadDraft);
                    }
                    catch (Exception exception)
                    {
                        LoggerHelper.Warning($"[刀帐] 自动识别任务失败：{exception.Message}");
                        await ShowRecognitionFailureAsync();
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

    [RelayCommand]
    private async Task Clear()
    {
        var result = await SukiMessageBox.ShowDialog(new SukiMessageBoxHost
        {
            Content = "确定要清空刀帐中的所有勾选状态吗？",
            ActionButtonsPreset = SukiMessageBoxButtons.YesNo,
            IconPreset = SukiMessageBoxIcons.Warning,
        }, new SukiMessageBoxOptions
        {
            Title = "清空刀帐",
        });

        if (!result.Equals(SukiMessageBoxResult.Yes))
            return;

        foreach (var row in Entries)
            row.ClearChecks();
        NotifySavedStateChanged();
    }

    partial void OnIsRecognizingChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    private static Task ShowRecognitionFailureAsync()
    {
        return DispatcherHelper.RunOnMainThreadAsync(() =>
        {
            _ = SukiMessageBox.ShowDialog(new SukiMessageBoxHost
            {
                Content = "请先将游戏页面切换至具体刀剑男士的刀帐页面，并确保顶部同时可识别到“序号”和数字，然后重新点击“自动识别”。",
                ActionButtonsPreset = SukiMessageBoxButtons.OK,
                IconPreset = SukiMessageBoxIcons.Warning,
            }, new SukiMessageBoxOptions
            {
                Title = "刀帐自动识别失败",
            });
        });
    }

    private void LoadCatalog()
    {
        var path = Path.Combine(AppPaths.ResourceDirectory, "base", "SwordBookCatalog.json");
        if (!File.Exists(path))
            return;
        var catalog = (JsonConvert.DeserializeObject<List<SwordBookCatalogItem>>(File.ReadAllText(path)) ?? [])
            .Where(item => !item.TypeOnly)
            .ToList();
        var duplicateNames = catalog.GroupBy(item => item.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => group.Last().Number, StringComparer.Ordinal);
        foreach (var item in catalog)
        {
            var displayName = duplicateNames.TryGetValue(item.Name, out var lastNumber) && lastNumber == item.Number
                ? $"{item.Name}·极"
                : item.Name;
            Entries.Add(new SwordBookRowViewModel(item.Number, item.Type, displayName, OnRowChanged));
        }
    }

    private void LoadSavedState()
    {
        var saved = ConfigurationManager.Current.GetValue<List<SwordBookPortraitState>>(ConfigurationKeys.SwordBookEntries, []);
        var savedByNumber = saved.ToDictionary(item => item.Number, StringComparer.Ordinal);
        _savedEntries.Clear();
        foreach (var row in Entries)
        {
            if (savedByNumber.TryGetValue(row.Number, out var state))
            {
                row.Apply(state);
                row.MarkSaved();
            }
            else
            {
                row.ClearChecks();
                row.MarkSaved();
            }
            _savedEntries[row.Number] = ToEntry(row).Clone();
        }
        NotifySavedStateChanged();
    }

    private void OnSwordBookDataSaved()
    {
        _ = DispatcherHelper.RunOnMainThreadAsync(LoadSavedState);
    }

    private void LoadDraft()
    {
        if (!File.Exists(DraftPath))
            return;
        var draft = JsonConvert.DeserializeObject<List<SwordBookPortraitState>>(File.ReadAllText(DraftPath)) ?? [];
        var draftByNumber = draft.ToDictionary(item => item.Number, StringComparer.Ordinal);
        foreach (var row in Entries)
            if (draftByNumber.TryGetValue(row.Number, out var state))
                row.Apply(state);
        NotifySavedStateChanged();
    }

    private void OnRowChanged() => NotifySavedStateChanged();
    private void NotifySavedStateChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        SaveCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
    }
    private static bool HasChanged(SwordBookRowViewModel row) =>
        row.Owned != row.SavedOwned || row.Wounded != row.SavedWounded || row.TrueSword != row.SavedTrueSword ||
        row.InnerCare != row.SavedInnerCare || row.Casual != row.SavedCasual;

    private static SwordBookEntry ToEntry(SwordBookRowViewModel row) => new(row.Number, row.Type, row.Name)
    {
        Owned = row.Owned,
        Wounded = row.Wounded, TrueSword = row.TrueSword, InnerCare = row.InnerCare, Casual = row.Casual,
    };

    private static SwordBookPortraitState ToState(SwordBookEntry entry) =>
        new(entry.Number, entry.Owned, entry.Wounded, entry.TrueSword, entry.InnerCare, entry.Casual);

    private sealed record SwordBookCatalogItem(string Number, string Type, string Name, bool TypeOnly = false);
}

public sealed partial class SwordBookRowViewModel : ObservableObject
{
    private readonly Action _changed;
    public SwordBookRowViewModel(string number, string type, string name, Action changed)
    {
        Number = number; Type = type; Name = name; _changed = changed;
    }
    public string Number { get; }
    public string Type { get; }
    public string Name { get; }
    public bool SavedWounded { get; private set; }
    public bool SavedOwned { get; private set; }
    public bool SavedTrueSword { get; private set; }
    public bool SavedInnerCare { get; private set; }
    public bool SavedCasual { get; private set; }
    [ObservableProperty] private bool _wounded;
    [ObservableProperty] private bool _owned;
    [ObservableProperty] private bool _trueSword;
    [ObservableProperty] private bool _innerCare;
    [ObservableProperty] private bool _casual;
    partial void OnWoundedChanged(bool value) => _changed();
    partial void OnOwnedChanged(bool value) => _changed();
    partial void OnTrueSwordChanged(bool value) => _changed();
    partial void OnInnerCareChanged(bool value) => _changed();
    partial void OnCasualChanged(bool value) => _changed();

    public void Apply(SwordBookEntry entry)
    {
        Owned = entry.Owned; Wounded = entry.Wounded; TrueSword = entry.TrueSword; InnerCare = entry.InnerCare; Casual = entry.Casual;
        MarkSaved();
    }
    public void Apply(SwordBookPortraitState state)
    {
        Owned = state.Owned; Wounded = state.Wounded; TrueSword = state.TrueSword; InnerCare = state.InnerCare; Casual = state.Casual;
    }
    public void MarkSaved()
    {
        SavedOwned = Owned; SavedWounded = Wounded; SavedTrueSword = TrueSword; SavedInnerCare = InnerCare; SavedCasual = Casual;
    }

    public void ClearChecks()
    {
        Owned = false; Wounded = false; TrueSword = false; InnerCare = false; Casual = false;
    }
}

public sealed record SwordBookPortraitState(string Number, bool Owned, bool Wounded, bool TrueSword, bool InnerCare, bool Casual);
