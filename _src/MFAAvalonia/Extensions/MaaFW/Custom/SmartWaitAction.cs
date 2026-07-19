using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using System;
using System.Linq;
using System.Threading;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 智能等待：读取 ExpeditionReturnTracker 的最早归队时间与全局 RefreshInterval，
/// 取较小值 sleep 后继续流水线。替代固定 post_delay 的 E_WaitRefresh。
/// </summary>
public class SmartWaitAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(SmartWaitAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            int intervalSeconds = GetRefreshIntervalSeconds();
            int remainingSeconds = ExpeditionReturnTracker.GetRemainingSeconds();

            int waitSeconds;
            if (remainingSeconds > 0)
            {
                waitSeconds = Math.Min(remainingSeconds, intervalSeconds);
                LoggerHelper.Info($"[远征计时] 最早 {remainingSeconds}s, 实际 {waitSeconds}s");
            }
            else
            {
                waitSeconds = intervalSeconds;
                LoggerHelper.Info($"[远征计时] 无远征, 间隔 {waitSeconds}s");
            }

            if (waitSeconds > 0)
            {
                // 分段 sleep 以响应停止信号
                var deadline = DateTime.Now.AddSeconds(waitSeconds);
                while (DateTime.Now < deadline)
                {
                    ActionParamHelper.ThrowIfStopping(context);
                    var chunk = (int)Math.Min(5, (deadline - DateTime.Now).TotalSeconds);
                    if (chunk <= 0) break;
                    Thread.Sleep(chunk * 1000);
                }
            }

            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[远征计时] 检测到手动停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[远征计时] 错误: {e.Message}");
            return false;
        }
    }

    /// <summary>从全局选项读取 RefreshInterval</summary>
    private static int GetRefreshIntervalSeconds()
    {
        try
        {
            var iface = MaaProcessor.Interface;
            var globalOpts = iface?.GlobalSelectOptions;
            var refreshOpt = globalOpts?.FirstOrDefault(o => o.Name == "RefreshInterval");
            if (refreshOpt?.Data != null &&
                refreshOpt.Data.TryGetValue("seconds", out var str) &&
                int.TryParse(str, out var val))
                return val;
        }
        catch { }
        return 600; // 默认 10 分钟
    }
}
