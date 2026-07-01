using System;
using System.Runtime.InteropServices;
using MFAAvalonia.Configuration;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Helper;

public static class SystemSleepHelper
{
    [Flags]
    private enum ExecutionState : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    public static void ApplyPreventSleep()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var prevent = GlobalConfiguration.GetValue(ConfigurationKeys.PreventSleep, "false").ToLower() == "true";
            ApplyPreventSleep(prevent);
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"应用防休眠设置失败：原因={ex.Message}", ex);
        }
    }

    public static void ApplyPreventSleep(bool prevent)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            if (prevent)
            {
                // Prevent system sleep, keep display on
                SetThreadExecutionState(ExecutionState.ES_CONTINUOUS | ExecutionState.ES_SYSTEM_REQUIRED | ExecutionState.ES_DISPLAY_REQUIRED);
                LoggerHelper.Info("已启用系统防休眠。");
            }
            else
            {
                // Allow sleep
                SetThreadExecutionState(ExecutionState.ES_CONTINUOUS);
                LoggerHelper.Info("已恢复系统休眠。");
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"设置系统执行状态失败：原因={ex.Message}", ex);
        }
    }
}
