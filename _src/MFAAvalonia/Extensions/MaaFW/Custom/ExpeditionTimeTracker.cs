using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using System;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 远征归队时间扫描：在队伍状态面板 OCR 五个部队的远征剩余时间，
/// 计算最早归队时刻存入 ExpeditionReturnTracker。
/// </summary>
public class ExpeditionTimeTracker : IMaaCustomAction
{
    public string Name { get; set; } = nameof(ExpeditionTimeTracker);

    /// <summary>五个部队剩余时间的 OCR ROI（可经由 action_param 覆盖）</summary>
    /// 与 E_CheckTeam1~5 的 OCR ROI 保持一致
    public static readonly int[][] DefaultRois =
    [
        [166, 38, 159, 31],   // 部队一
        [166, 152, 159, 31],  // 部队二
        [166, 268, 159, 31],  // 部队三
        [166, 383, 159, 31],  // 部队四
        [166, 498, 159, 31],  // 部队五
    ];

    /// <summary>关闭面板的点击坐标（target [230,12,71,19] + offset [6,4]）</summary>
    public const int ClosePanelX = 236;
    public const int ClosePanelY = 16;

    /// <summary>
    /// 扫描队伍状态面板，计算最早归队剩余秒数并存入 ExpeditionReturnTracker。
    /// 供 ExpeditionTimerAction 在智能调度时复用。
    /// </summary>
    /// <returns>最早归队剩余秒数，无远征时返回 null</returns>
    public static int? ScanAndStore<T>(T context) where T : IMaaContext
    {
        int? minRemaining = null;

        using var image = context.GetImage();
        for (int i = 0; i < 5; i++)
        {
            var roi = DefaultRois[i];
            var text = image != null
                ? context.GetText(roi[0], roi[1], roi[2], roi[3], image)
                : null;
            LoggerHelper.Info($"[远征扫描] 部队{i + 1} OCR: '{text}'");

            var seconds = ParseRemainingSeconds(text);
            if (seconds.HasValue && (!minRemaining.HasValue || seconds.Value < minRemaining.Value))
                minRemaining = seconds.Value;
        }

        if (minRemaining.HasValue && minRemaining.Value > 0)
        {
            var returnTime = DateTime.Now.AddSeconds(minRemaining.Value + 10);
            ExpeditionReturnTracker.SetEarliestReturn(returnTime);
            LoggerHelper.Info($"[远征扫描] 最早归队: {returnTime:HH:mm:ss}（{minRemaining.Value}秒 + 10秒缓冲）");
        }
        else
        {
            ExpeditionReturnTracker.Reset();
            LoggerHelper.Info("[远征扫描] 无进行中远征，重置追踪器");
        }

        return minRemaining;
    }

    /// <summary>检查全局开关"远征智能调度"是否开启</summary>
    public static bool IsSmartSchedulingEnabled()
    {
        try
        {
            var iface = MaaProcessor.Interface;
            var globalOpts = iface?.GlobalSelectOptions;
            var smartOpt = globalOpts?.FirstOrDefault(o => o.Name == "远征智能调度");
            // switch 类型通过 Index 判断（0=Yes/开启, 非0=No/关闭），不能用 SelectedCases
            return smartOpt?.Index == 0;
        }
        catch { return false; }
    }

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            ScanAndStore(context);

            // 点击关闭队伍状态面板
            context.Click(ClosePanelX, ClosePanelY);
            LoggerHelper.Info("[远征扫描] 已点击关闭面板");

            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[远征扫描] 检测到手动停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[远征扫描] 错误: {e.Message}");
            return false;
        }
    }

    /// <summary>解析剩余时间文本："待机中"→null，"远征中（01：23：45）"→秒数</summary>
    public static int? ParseRemainingSeconds(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var clean = text.Trim();

        // 待机/空闲 = 无远征
        if (clean.Contains("待机") || clean.Contains("空闲"))
            return null;

        // 从"远征中（HH：MM：SS）"中提取时间部分
        var timePart = clean;
        var leftParen = clean.IndexOf('（'); // 全角左括号
        if (leftParen < 0) leftParen = clean.IndexOf('(');
        var rightParen = clean.LastIndexOf('）'); // 全角右括号
        if (rightParen < 0) rightParen = clean.LastIndexOf(')');
        if (leftParen >= 0 && rightParen > leftParen)
            timePart = clean.Substring(leftParen + 1, rightParen - leftParen - 1).Trim();

        // 统一全角冒号→半角
        timePart = timePart.Replace('：', ':');

        // 尝试 HH:MM:SS 或 H:MM:SS
        var parts = timePart.Split(':');
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out var h) &&
            int.TryParse(parts[1], out var m) &&
            int.TryParse(parts[2], out var s))
        {
            return h * 3600 + m * 60 + s;
        }

        // 尝试 MM:SS
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var m2) &&
            int.TryParse(parts[1], out var s2))
        {
            return m2 * 60 + s2;
        }

        return null;
    }
}
