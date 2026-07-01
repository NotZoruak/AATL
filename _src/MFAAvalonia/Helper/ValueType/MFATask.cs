using CommunityToolkit.Mvvm.ComponentModel;
using MaaFramework.Binding;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.ViewModels.Pages;
using MFAAvalonia.Views.Windows;
using MFAAvalonia.Helper;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.Helper.ValueType;

public partial class MFATask : ObservableObject
{
    public enum MFATaskType
    {
        MFA,
        MAAFW
    }

    public enum MFATaskStatus
    {
        NOT_STARTED,
        STOPPING,
        STOPPED,
        SUCCEEDED,
        FAILED
    }

    [ObservableProperty] private string? _name = string.Empty;
    [ObservableProperty] private MFATaskType _type = MFATaskType.MFA;
    [ObservableProperty] private int _count = 1;
    [ObservableProperty] private Func<Task> _action;
    // [ObservableProperty] private Dictionary<string, MaaNode> _tasks = new();
    [ObservableProperty] private bool _isUpdateRelated;

    public TaskQueueViewModel? OwnerViewModel { get; set; }

    public async Task<MFATaskStatus> Run(CancellationToken token)
    {
        try
        {
            if (Count < 0)
                Count = int.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Type == MFATaskType.MAAFW)
                {
                    OwnerViewModel?.AddLogByKey(LangKeys.TaskStart, (Avalonia.Media.IBrush?)null, true, true, LanguageHelper.GetLocalizedString(Name));
                    OwnerViewModel?.SetCurrentTaskName(LanguageHelper.GetLocalizedString(Name));
                }
                await Action();
            }
            return MFATaskStatus.SUCCEEDED;
        }
        catch (MaaJobStatusException)
        {
            LoggerHelper.Error($"任务执行失败：{LanguageHelper.GetLocalizedString(Name)}");
            return MFATaskStatus.FAILED;
        }
        catch (OperationCanceledException)
        {
            return MFATaskStatus.STOPPED;
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"任务执行异常：任务={LanguageHelper.GetLocalizedString(Name)}，原因={ex.Message}", ex);
            return MFATaskStatus.FAILED;
        }
    }
}
