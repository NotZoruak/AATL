using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using MFAAvalonia.Extensions.MaaFW;
using Newtonsoft.Json.Linq;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class DungeonFloorSelectAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(DungeonFloorSelectAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var json = ActionParamHelper.Parse(args.ActionParam);
            int targetFloor = (int?)json["target_floor"] ?? 51;
            targetFloor = Math.Clamp(targetFloor, 1, 99);

            int targetTens = targetFloor / 10;
            int targetOnes = targetFloor % 10;

            var tensRoi = ParseRoi(json, "tens_roi");
            var onesRoi = ParseRoi(json, "ones_roi");
            var tensUp = ParseRoi(json, "tens_up");
            var tensDown = ParseRoi(json, "tens_down");
            var onesUp = ParseRoi(json, "ones_up");
            var onesDown = ParseRoi(json, "ones_down");
            var colorConfig = ParseDigitColorConfig(json);

            LoggerHelper.Info($"[DungeonFloorSelect] 目标层数: {targetFloor} (十位={targetTens}, 个位={targetOnes})");

            // 先读一次十位和个位
            int currentTens = ReadDigitStable(context, tensRoi, "十位", colorConfig);
            int currentOnes = ReadDigitStable(context, onesRoi, "个位", colorConfig);
            LoggerHelper.Info($"[DungeonFloorSelect] 当前层数: {currentTens}{currentOnes}");

            // 调整十位（不循环，只能单向）
            if (currentTens != targetTens)
            {
                int realClicks = currentTens > targetTens ? currentTens - targetTens : targetTens - currentTens;
                bool goDown = currentTens > targetTens;
                int btnX = goDown ? tensDown[0] + tensDown[2] / 2 : tensUp[0] + tensUp[2] / 2;
                int btnY = goDown ? tensDown[1] + tensDown[3] / 2 : tensUp[1] + tensUp[3] / 2;
                LoggerHelper.Info($"[DungeonFloorSelect] 十位 {currentTens}→{targetTens}，{(goDown ? "减" : "加")}{realClicks}次");
                for (int i = 0; i < realClicks; i++)
                {
                    context.Click(btnX, btnY);
                    ActionParamHelper.SleepWithStopCheck(context, 250);
                }
                ActionParamHelper.SleepWithStopCheck(context, 500);
            }

            // 再读一次个位（十位调整可能影响显示）
            ActionParamHelper.SleepWithStopCheck(context, 300);
            currentOnes = ReadDigitStable(context, onesRoi, "个位", colorConfig);

            // 调整个位（不循环，只能单向）
            if (currentOnes != targetOnes)
            {
                int realClicks = currentOnes > targetOnes ? currentOnes - targetOnes : targetOnes - currentOnes;
                bool goDown = currentOnes > targetOnes;
                int btnX = goDown ? onesDown[0] + onesDown[2] / 2 : onesUp[0] + onesUp[2] / 2;
                int btnY = goDown ? onesDown[1] + onesDown[3] / 2 : onesUp[1] + onesUp[3] / 2;
                LoggerHelper.Info($"[DungeonFloorSelect] 个位 {currentOnes}→{targetOnes}，{(goDown ? "减" : "加")}{realClicks}次");
                for (int i = 0; i < realClicks; i++)
                {
                    context.Click(btnX, btnY);
                    ActionParamHelper.SleepWithStopCheck(context, 250);
                }
                ActionParamHelper.SleepWithStopCheck(context, 500);
            }

            // 验证
            ActionParamHelper.SleepWithStopCheck(context, 300);
            int finalTens = ReadDigitStable(context, tensRoi, "十位验证", colorConfig);
            int finalOnes = ReadDigitStable(context, onesRoi, "个位验证", colorConfig);
            LoggerHelper.Info($"[DungeonFloorSelect] 验证结果: {finalTens}{finalOnes}");

            if (finalTens == targetTens && finalOnes == targetOnes)
            {
                LoggerHelper.Info("[DungeonFloorSelect] 层数选择完成");
                return true;
            }

            // 验证不通过，用逐次 OCR 方式微调
            LoggerHelper.Info("[DungeonFloorSelect] 盲点未到位，进入逐次微调模式");
            AdjustDigitLoop(context, tensRoi, targetTens, tensUp, tensDown, "十位", colorConfig);
            AdjustDigitLoop(context, onesRoi, targetOnes, onesUp, onesDown, "个位", colorConfig);
            LoggerHelper.Info("[DungeonFloorSelect] 层数选择完成");
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[DungeonFloorSelect] 手动停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[DungeonFloorSelect] Error: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 稳定读取一位数字，内部处理重试
    /// </summary>
    private int ReadDigitStable(IMaaContext context, int[] roi, string label, DigitColorConfig colorConfig)
    {
        const int requiredConsecutiveReads = 3;
        const int maxAttempts = 15;
        int? previousDigit = null;
        int consecutiveReads = 0;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);

            using var image = context.GetImage();
            if (image == null)
            {
                ActionParamHelper.SleepWithStopCheck(context, 400);
                continue;
            }

            var ocrRoi = FindDigitColorRoi(context, image, roi, colorConfig, label);
            var text = context.GetText(ocrRoi[0], ocrRoi[1], ocrRoi[2], ocrRoi[3], image);
            int? digit = ParseDigit(text);
            if (digit.HasValue)
            {
                if (previousDigit == digit)
                    consecutiveReads++;
                else
                {
                    previousDigit = digit;
                    consecutiveReads = 1;
                }

                LoggerHelper.Info($"[DungeonFloorSelect] {label} OCR 识别: {digit}，连续一致 {consecutiveReads}/{requiredConsecutiveReads}");
                if (consecutiveReads >= requiredConsecutiveReads)
                    return digit.Value;
            }
            else
            {
                previousDigit = null;
                consecutiveReads = 0;
                LoggerHelper.Info($"[DungeonFloorSelect] {label} OCR 结果无效: '{text}'，连续计数已清零");
            }

            if (attempt < maxAttempts - 1)
                ActionParamHelper.SleepWithStopCheck(context, 250);
        }

        throw new Exception($"{label} OCR 识别失败（未能连续{requiredConsecutiveReads}次得到一致结果，已尝试{maxAttempts}次）");
    }

    /// <summary>
    /// 逐次 OCR 微调模式（盲点未到位时的后备方案）
    /// </summary>
    private void AdjustDigitLoop(IMaaContext context, int[] roi, int target, int[] up, int[] down, string label, DigitColorConfig colorConfig)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);

            int current = ReadDigitStable(context, roi, label, colorConfig);
            if (current == target)
            {
                LoggerHelper.Info($"[DungeonFloorSelect] {label}已匹配: {current} == {target}");
                return;
            }

            int downClicks = (current - target + 10) % 10;
            int upClicks = (target - current + 10) % 10;
            bool goDown = downClicks <= upClicks;

            int clickX = goDown ? down[0] + down[2] / 2 : up[0] + up[2] / 2;
            int clickY = goDown ? down[1] + down[3] / 2 : up[1] + up[3] / 2;
            LoggerHelper.Info($"[DungeonFloorSelect] {label} {current}→{target}，{'减'}/{'加'} ({clickX}, {clickY})");

            context.Click(clickX, clickY);
            ActionParamHelper.SleepWithStopCheck(context, 800);
        }

        throw new Exception($"{label}微调失败，已重试10次");
    }

    private static int[] ParseRoi(JObject json, string key)
    {
        var arr = json[key] as JArray;
        if (arr == null || arr.Count != 4)
            throw new Exception($"参数 {key} 格式错误，需要 [x, y, w, h]");
        return new[] { (int)arr[0], (int)arr[1], (int)arr[2], (int)arr[3] };
    }

    private static DigitColorConfig ParseDigitColorConfig(JObject json)
    {
        return new DigitColorConfig(
            ParseColor(json, "digit_color_upper", [255, 255, 255]),
            ParseColor(json, "digit_color_lower", [160, 160, 160]),
            (int?)json["digit_color_count"] ?? 3);
    }

    private static int[] ParseColor(JObject json, string key, int[] defaultValue)
    {
        var arr = json[key] as JArray;
        if (arr == null || arr.Count != 3)
            return defaultValue;
        return new[] { (int)arr[0], (int)arr[1], (int)arr[2] };
    }

    private static int? ParseDigit(string? text)
    {
        text = text?.Trim();
        if (text is "O" or "o")
            text = "0";
        if (int.TryParse(text, out int digit) && digit is >= 0 and <= 9)
            return digit;
        return null;
    }

    private static int[] FindDigitColorRoi<T>(T context, IMaaImageBuffer image, int[] roi, DigitColorConfig colorConfig, string label)
        where T : IMaaContext
    {
        if (!context.ColorMatch(
                colorConfig.Upper[0], colorConfig.Upper[1], colorConfig.Upper[2],
                colorConfig.Lower[0], colorConfig.Lower[1], colorConfig.Lower[2],
                image, out var detail, 0.8, roi[0], roi[1], roi[2], roi[3], colorConfig.Count)
            || detail?.HitBox == null)
        {
            return roi;
        }

        // 命中框向四周扩展，避免颜色匹配只覆盖笔画的一部分而截断数字。
        const int padding = 4;
        int x = Math.Max(roi[0], detail.HitBox.X - padding);
        int y = Math.Max(roi[1], detail.HitBox.Y - padding);
        int right = Math.Min(roi[0] + roi[2], detail.HitBox.X + detail.HitBox.Width + padding);
        int bottom = Math.Min(roi[1] + roi[3], detail.HitBox.Y + detail.HitBox.Height + padding);
        int width = right - x;
        int height = bottom - y;
        if (width <= 0 || height <= 0)
            return roi;

        LoggerHelper.Info($"[DungeonFloorSelect] {label} ColorMatch 命中，OCR ROI: [{x},{y},{width},{height}]");
        return new[] { x, y, width, height };
    }

    private sealed record DigitColorConfig(int[] Upper, int[] Lower, int Count);
}
