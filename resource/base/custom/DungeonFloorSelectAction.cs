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

            AdjustDigit(context, tensRoi, targetTens, tensUp, tensDown, "十位");
            AdjustDigit(context, onesRoi, targetOnes, onesUp, onesDown, "个位");

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

    private void AdjustDigit(IMaaContext context, int[] roi, int target, int[] up, int[] down, string label)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);

            int current = ReadDigit(context, roi, label);
            if (current == target)
            {
                LoggerHelper.Info($"[DungeonFloorSelect] {label}已匹配: {current} == {target}");
                return;
            }

            int clickX, clickY;
            if (current < target)
            {
                clickX = up[0] + up[2] / 2;
                clickY = up[1] + up[3] / 2;
                LoggerHelper.Info($"[DungeonFloorSelect] {label} {current} < {target}，点击加号 ({clickX}, {clickY})");
            }
            else
            {
                clickX = down[0] + down[2] / 2;
                clickY = down[1] + down[3] / 2;
                LoggerHelper.Info($"[DungeonFloorSelect] {label} {current} > {target}，点击减号 ({clickX}, {clickY})");
            }

            context.Click(clickX, clickY);
            ActionParamHelper.SleepWithStopCheck(context, 400);
        }

        LoggerHelper.Error($"[DungeonFloorSelect] {label}调整失败，已重试20次");
    }

    private int ReadDigit(IMaaContext context, int[] roi, string label)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);

            using var image = context.GetImage();
            if (image == null)
            {
                ActionParamHelper.SleepWithStopCheck(context, 200);
                continue;
            }

            var text = context.GetText(roi[0], roi[1], roi[2], roi[3], image);
            if (int.TryParse(text, out int digit) && digit >= 0 && digit <= 9)
            {
                LoggerHelper.Info($"[DungeonFloorSelect] {label} OCR 识别: {digit}");
                return digit;
            }

            LoggerHelper.Info($"[DungeonFloorSelect] {label} OCR 结果无效: '{text}'，重试 {attempt + 1}/3");
            ActionParamHelper.SleepWithStopCheck(context, 200);
        }

        throw new Exception($"{label} OCR 识别失败（已重试3次）");
    }

    private static int[] ParseRoi(JObject json, string key)
    {
        var arr = json[key] as JArray;
        if (arr == null || arr.Count != 4)
            throw new Exception($"参数 {key} 格式错误，需要 [x, y, w, h]");
        return new[] { (int)arr[0], (int)arr[1], (int)arr[2], (int)arr[3] };
    }
}
