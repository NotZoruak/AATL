using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
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

            LoggerHelper.Info($"[DungeonFloorSelect] 目标层数: {targetFloor} (十位={targetTens}, 个位={targetOnes})");

            // 先读一次十位和个位
            int currentTens = ReadDigitStable(context, tensRoi, "十位");
            int currentOnes = ReadDigitStable(context, onesRoi, "个位");
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
            currentOnes = ReadDigitStable(context, onesRoi, "个位");

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
            int finalTens = ReadDigitStable(context, tensRoi, "十位验证");
            int finalOnes = ReadDigitStable(context, onesRoi, "个位验证");
            LoggerHelper.Info($"[DungeonFloorSelect] 验证结果: {finalTens}{finalOnes}");

            if (finalTens == targetTens && finalOnes == targetOnes)
            {
                LoggerHelper.Info("[DungeonFloorSelect] 层数选择完成");
                return true;
            }

            // 验证不通过，用逐次 OCR 方式微调
            LoggerHelper.Info("[DungeonFloorSelect] 盲点未到位，进入逐次微调模式");
            AdjustDigitLoop(context, tensRoi, targetTens, tensUp, tensDown, "十位");
            AdjustDigitLoop(context, onesRoi, targetOnes, onesUp, onesDown, "个位");
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
    private int ReadDigitStable(IMaaContext context, int[] roi, string label)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);

            using var image = context.GetImage();
            if (image == null)
            {
                ActionParamHelper.SleepWithStopCheck(context, 400);
                continue;
            }

            var text = context.GetText(roi[0], roi[1], roi[2], roi[3], image);
            // OCR 可能把 0 识别成字母 O
            if (text == "O" || text == "o")
                text = "0";
            if (int.TryParse(text, out int digit) && digit >= 0 && digit <= 9)
            {
                LoggerHelper.Info($"[DungeonFloorSelect] {label} OCR 识别: {digit}");
                return digit;
            }

            LoggerHelper.Info($"[DungeonFloorSelect] {label} OCR 结果无效: '{text}'，重试 {attempt + 1}/8");
            ActionParamHelper.SleepWithStopCheck(context, 400);
        }

        throw new Exception($"{label} OCR 识别失败（已重试8次）");
    }

    /// <summary>
    /// 逐次 OCR 微调模式（盲点未到位时的后备方案）
    /// </summary>
    private void AdjustDigitLoop(IMaaContext context, int[] roi, int target, int[] up, int[] down, string label)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);

            int current = ReadDigitStable(context, roi, label);
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
}
