using System.Collections.Generic;

namespace MFAAvalonia.Helper;

public static class TaskQueueContinuationPolicy
{
    /// <summary>由 MFAA 提供的特殊任务 Entry。</summary>
    public static readonly HashSet<string> SpecialActionNames = new()
    {
        "CountdownAction",
        "TimedWaitAction",
        "SystemNotificationAction",
        "CustomProgramAction",
        "KillProcessAction",
        "ComputerOperationAction",
        "WebhookAction",
        "SwitchInstanceAction",
    };

    /// <summary>只有下一个任务不是特殊任务时，才需要在任务之间回本丸。</summary>
    public static bool ShouldInsertGoHome(string? nextTaskEntry) =>
        !string.IsNullOrWhiteSpace(nextTaskEntry) && !SpecialActionNames.Contains(nextTaskEntry);

    public static bool CanContinue(bool taskFailed, bool continueOnError) =>
        taskFailed && continueOnError;
}
