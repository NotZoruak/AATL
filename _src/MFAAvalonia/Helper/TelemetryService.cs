using System;

namespace MFAAvalonia.Helper;

/// <summary>
/// MATR 禁用遥测后的兼容入口。
/// 所有方法均不采集、不保存且不发送任何运行信息。
/// </summary>
public static class TelemetryService
{
    public static void InitializeBootstrapFromInterface(string dataRoot) { }

    public static void InitializeFromInterface() { }

    public static void Shutdown() { }

    public static void SetEnabled(bool enabled) { }

    public static void CaptureException(Exception exception, string source) { }

    public static void CaptureStartupException(Exception exception) { }

    public static void StartRun(string instanceId, object? tasks, object? controllerName, object? controllerType) { }

    public static void FinishRun(string instanceId, object? status) { }

    public static void StartTask(string instanceId, object? task) { }

    public static void FinishTask(string instanceId, object? task, object? status, bool failed) { }

    public static void RecordTaskFailure(string instanceId, string failureCode, string stage, object? detail) { }

    public static void RecordNodeEvent(string instanceId, string message, object? details, bool traced) { }

    public static void SetActiveTaskId(string instanceId, long taskId) { }
}
