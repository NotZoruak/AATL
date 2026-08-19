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
    /// <summary>每轮执行成功后的回调，参数为当前任务已完成的轮次。</summary>
    public Action<int>? IterationCompleted { get; set; }
    // [ObservableProperty] private Dictionary<string, MaaNode> _tasks = new();
    [ObservableProperty] private bool _isUpdateRelated;

    public TaskQueueViewModel? OwnerViewModel { get; set; }

    public async Task<MFATaskStatus> Run(CancellationToken token)
    {
        try
        {
            var infinite = Count < 0;   // 无限重复标记，先记录再转 int.MaxValue
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
                IterationCompleted?.Invoke(i + 1);
                // 有限重复的 MAAFW 任务每轮结束后报告进度；无限重复与单次任务不报
                if (!infinite && Count > 1 && Type == MFATaskType.MAAFW)
                {
                    OwnerViewModel?.AddLogByKey(LangKeys.TaskRoundComplete, (Avalonia.Media.IBrush?)null, true, true,
                        LanguageHelper.GetLocalizedString(Name), (i + 1).ToString(), Count.ToString());
                }
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
