using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 长期远征计划——疲劳值检测。
/// 两套 ROI：远征队伍面板（check_all）和出阵编队页面（check_captain）。
/// 游戏中的"疲劳值"实际是心情值，越高越好。OCR 结果格式为 XX/100。
/// </summary>
public class FatigueCheckAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(FatigueCheckAction);

    /// <summary>远征队伍面板——六个疲劳 OCR ROI</summary>
    public static readonly int[][] FatigueRoisExpedition =
    [
        [839, 190, 77, 22], // 位置一（队长）
        [839, 284, 77, 22], // 位置二
        [839, 379, 77, 22], // 位置三
        [839, 473, 77, 22], // 位置四
        [839, 568, 77, 22], // 位置五
        [839, 662, 77, 22], // 位置六
    ];

    /// <summary>出阵编队页面——六个疲劳 OCR ROI（复用 DragCaptainAction）</summary>
    public static readonly int[][] FatigueRoisSortie =
    [
        [340, 187, 80, 22], // 位置一（队长）
        [340, 282, 80, 22], // 位置二
        [340, 376, 80, 22], // 位置三
        [340, 471, 80, 22], // 位置四
        [340, 565, 80, 22], // 位置五
        [340, 660, 80, 22], // 位置六
    ];

    /// <summary>OCR 六个位置的疲劳值，空槽位或失败返回 null。返回 [0..5] 对应位置一~六</summary>
    public static int?[] ReadFatigue<T>(T context, int[][] rois) where T : IMaaContext
    {
        var values = new int?[6];
        using var image = context.GetImage();
        if (image == null) return values;

        for (int i = 0; i < 6; i++)
        {
            var roi = rois[i];
            var text = context.GetText(roi[0], roi[1], roi[2], roi[3], image);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var clean = text.Trim().Replace('B', '8').Replace('O', '0').Replace('S', '5');
                if (clean.Contains('/')) clean = clean.Split('/')[0];
                if (int.TryParse(clean.Trim(), out var val) && val >= 0)
                    values[i] = val;
            }
        }
        return values;
    }

    /// <summary>找最低疲劳值的索引和值。无可用位置返回 (-1, -1)</summary>
    public static (int Index, int Value) FindLowest(int?[] values)
    {
        int bestPos = -1, bestVal = int.MaxValue;
        for (int i = 0; i < 6; i++)
        {
            if (!values[i].HasValue) continue;
            if (values[i].Value < bestVal) { bestVal = values[i].Value; bestPos = i; }
        }
        return (bestPos, bestVal);
    }

    /// <summary>获取用户阈值，默认 91</summary>
    public static int GetThreshold()
    {
        var globalOpts = MaaProcessor.Interface?.GlobalSelectOptions;
        var fatigueOpt = globalOpts?.FirstOrDefault(o => o.Name == "疲劳阈值");
        if (fatigueOpt?.SelectedCases != null && fatigueOpt.SelectedCases.Count > 0
            && int.TryParse(fatigueOpt.SelectedCases[0], out var t) && t > 0)
            return t;
        return 91;
    }

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            var json = ActionParamHelper.Parse(args.ActionParam);
            var mode = (string?)json["mode"] ?? "check_all";
            var threshold = (int?)json["threshold"] ?? GetThreshold();

            var rois = mode == "check_captain" ? FatigueRoisSortie : FatigueRoisExpedition;
            var values = ReadFatigue(context, rois);
            var (bestPos, bestVal) = FindLowest(values);

            LoggerHelper.Info($"[疲劳检测] mode={mode}, 阈值={threshold}");
            LoggerHelper.Info($"[疲劳检测] 六位疲劳: [{string.Join(", ", values.Select(v => v?.ToString() ?? "空"))}]");

            var team = (int?)json["team"] ?? 0;

            if (mode == "check_captain")
            {
                if (!values[0].HasValue) { LoggerHelper.Warning("[疲劳检测] 队长位 OCR 失败"); return false; }
                var ok = values[0].Value >= threshold;
                LoggerHelper.Info($"[疲劳检测] 首位={values[0]}, > {threshold}? {ok}");
                FlowerStateTracker.CurrentFatigueLowest = values[0].Value;
                return ok;
            }
            else
            {
                if (bestPos < 0) { LoggerHelper.Info("[疲劳检测] 全空槽位，视为合格"); return true; }
                var ok = bestVal >= threshold;
                LoggerHelper.Info($"[疲劳检测] 最低位=位置{bestPos + 1}, 值={bestVal}, > {threshold}? {ok}");
                FlowerStateTracker.CurrentFatigueLowest = bestVal;
                if (!ok)
                {
                    if (team > 0) FlowerStateTracker.BeginTeam(team);
                    try { MaaProcessorManager.Instance.Current?.AddLog($"[远征疲劳检测] 有刀剑疲劳低于阈值，进入刷花"); } catch { }
                }
                return ok;
            }
        }
        catch (MaaStopException) { LoggerHelper.Info("[疲劳检测] 手动停止"); return false; }
        catch (Exception e) { LoggerHelper.Error($"[疲劳检测] 错误: {e.Message}"); return false; }
    }
}
