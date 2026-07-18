using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 队内拖拽换队长：OCR 六个位置的疲劳值，找最低值（排除跳过位置），长按+拖拽到目标槽位
/// </summary>
public class DragCaptainAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(DragCaptainAction);

    /// <summary>六个位置的疲劳值 OCR ROI [x, y, w, h]</summary>
    private static readonly int[][] FatigueRois =
    [
        [340, 187, 80, 22], // 位置一（队长槽）
        [340, 282, 80, 22], // 位置二
        [340, 376, 80, 22], // 位置三
        [340, 471, 80, 22], // 位置四
        [340, 565, 80, 22], // 位置五
        [340, 660, 80, 22], // 位置六
    ];

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            var tasker = context.Tasker;

            // 读取跳过位置配置
            var skipPositions = GetSkipPositions();

            // OCR 六个位置的疲劳值，空槽位标记为不可用
            var fatigueValues = new int?[6];
            using var image = context.GetImage();
            for (int i = 0; i < 6; i++)
            {
                if (skipPositions.Contains(i)) continue; // 跳过位置不 OCR
                var roi = FatigueRois[i];
                var text = image != null ? context.GetText(roi[0], roi[1], roi[2], roi[3], image) : null;
                LoggerHelper.Info($"[DragCaptain] 位置{i+1} OCR原始: '{text}'");
                if (text != null)
                {
                    var clean = text.Trim().Replace('B', '8').Replace('O', '0').Replace('S', '5');
                    if (clean.Contains('/')) clean = clean.Split('/')[0];
                    if (int.TryParse(clean.Trim(), out var val) && val >= 0)
                        fatigueValues[i] = val;
                }
                // 空槽位或 OCR 失败保持 null，不参与比较
            }

            // 找可用位置中疲劳值最低的
            int bestPos = -1;
            int bestVal = int.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                if (!fatigueValues[i].HasValue) continue;
                if (fatigueValues[i].Value < bestVal)
                {
                    bestVal = fatigueValues[i].Value;
                    bestPos = i;
                }
            }

            if (bestPos < 0)
            {
                LoggerHelper.Warning("[DragCaptain] 无可用位置（空槽位或 OCR 失败），跳过拖拽");
                return true;
            }

            LoggerHelper.Info($"[DragCaptain] 疲劳值: [{string.Join(",", fatigueValues)}], 最低位置: {bestPos + 1} (值={bestVal})");

            // 跳过位置一时自动跳过位置二，位置二成为新的"队长槽"
            if (skipPositions.Contains(0)) skipPositions.Add(1);
            if (bestPos == 0 || (bestPos == 1 && skipPositions.Contains(1)))
            {
                LoggerHelper.Info($"[DragCaptain] 最低疲劳在位置{bestPos + 1}，跳过拖拽");
                return true;
            }

            // 跳过位置一时自动跳过位置二，位置二成为新的"队长槽"
            if (skipPositions.Contains(0)) skipPositions.Add(1);
            int targetPos = skipPositions.Contains(0) ? 1 : 0;
            var src = FatigueRois[bestPos];
            var dst = FatigueRois[targetPos];
            int srcCx = src[0] + src[2] / 2;
            int srcCy = src[1] + src[3] / 2;
            int dstCx = dst[0] + dst[2] / 2;
            int dstCy = dst[1] + dst[3] / 2;

            LoggerHelper.Info($"[DragCaptain] 拖拽: 位置{bestPos + 1}({srcCx},{srcCy}) → 位置{targetPos + 1}({dstCx},{dstCy})");

            // 长按 1s
            tasker.TouchDown(0, srcCx, srcCy, 1);
            Thread.Sleep(1000);
            // 拖拽 500ms
            tasker.Swipe(srcCx, srcCy, dstCx, dstCy, 500);

            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[DragCaptain] 检测到手动停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[DragCaptain] 错误: {e.Message}");
            return false;
        }
    }

    /// <summary>从全局选项读取跳过位置配置，返回 0-based 索引集合</summary>
    private static HashSet<int> GetSkipPositions()
    {
        var skipSet = new HashSet<int>();
        var iface = MaaProcessor.Interface;
        var globalOpts = iface?.GlobalSelectOptions;
        if (globalOpts == null) return skipSet;

        // 「拖拽跳过位置」是「换队长方式」的子选项，需穿透 SubOptions 查找
        var captainOpt = globalOpts.FirstOrDefault(o => o.Name == "换队长方式");
        var dragSkipOpt = captainOpt?.SubOptions?.FirstOrDefault(o => o.Name == "拖拽跳过位置");
        if (dragSkipOpt?.SelectedCases == null) return skipSet;

        foreach (var name in dragSkipOpt.SelectedCases)
        {
            if (name == "位置一") skipSet.Add(0);
            else if (name == "位置二") skipSet.Add(1);
            else if (name == "位置三") skipSet.Add(2);
            else if (name == "位置四") skipSet.Add(3);
            else if (name == "位置五") skipSet.Add(4);
            else if (name == "位置六") skipSet.Add(5);
        }
        return skipSet;
    }
}
