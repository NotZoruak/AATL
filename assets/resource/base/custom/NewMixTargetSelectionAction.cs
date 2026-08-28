using Avalonia;
using Avalonia.Media.Imaging;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>扫描新习合列表前六个位置，选择锁刀或乱舞等级低于7级的刀剑。</summary>
public sealed class NewMixTargetSelectionAction : IMaaCustomAction
{
    private static readonly int[][] MarkerRois =
    [
        [88, 127, 7, 8],
        [88, 228, 7, 8],
        [88, 329, 7, 8],
        [88, 430, 7, 8],
        [88, 531, 7, 8],
        [88, 632, 7, 8]
    ];

    private static readonly int[][] LevelRois =
    [
        [604, 146, 38, 22],
        [604, 247, 38, 22],
        [604, 348, 38, 22],
        [604, 449, 38, 22],
        [604, 550, 38, 22],
        [604, 651, 38, 22]
    ];

    private static readonly int[][] SelectTargets =
    [
        [1216, 164],
        [1216, 265],
        [1216, 366],
        [1216, 467],
        [1216, 568],
        [1216, 669]
    ];

    private const byte EmptyLower = 218;
    private const byte EmptyUpper = 222;
    private const byte LockedR = 212;
    private const byte LockedG = 173;
    private const byte LockedB = 31;
    private const byte ColorTolerance = 2;

    public string Name { get; set; } = nameof(NewMixTargetSelectionAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            var slots = ReadSlots(context);
            var plan = NewMixTargetSelectionDecision.Decide(slots);
            NewMixTargetSelectionState.Current = plan;

            LoggerHelper.Info($"[新习合] 目标扫描结果：{plan.Outcome}，位置：{plan.Position}");
            if (plan.Outcome is NewMixTargetSelectionOutcome.NoSword or NewMixTargetSelectionOutcome.Completed)
                return true;

            if (plan.Outcome == NewMixTargetSelectionOutcome.Unreadable)
            {
                LoggerHelper.Warning($"[新习合] 第 {plan.Position} 位乱舞等级无法识别，停止本轮选择");
                return false;
            }

            var target = SelectTargets[plan.Position - 1];
            context.Click(target[0], target[1]);
            LoggerHelper.Info($"[新习合] 点击第 {plan.Position} 位选择按钮：({target[0]},{target[1]})");
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[新习合] 手动停止目标扫描");
            return false;
        }
        catch (Exception e)
        {
            NewMixTargetSelectionState.Current = new(NewMixTargetSelectionOutcome.Unreadable, 0);
            LoggerHelper.Error($"[新习合] 目标扫描异常：{e.Message}");
            return false;
        }
    }

    private static List<NewMixTargetSlot> ReadSlots<T>(T context) where T : IMaaContext
    {
        var slots = new List<NewMixTargetSlot>(MarkerRois.Length);
        for (var index = 0; index < MarkerRois.Length; index++)
        {
            ActionParamHelper.ThrowIfStopping(context);
            using var image = context.GetImage();
            using var bitmap = image?.ToBitmap();
            if (bitmap == null)
                throw new InvalidOperationException("无法获取习合目标列表截图");

            var hasSword = !ContainsGray(bitmap, MarkerRois[index], EmptyLower, EmptyUpper);
            var isLocked = hasSword && ContainsColor(bitmap, MarkerRois[index], LockedR, LockedG, LockedB, ColorTolerance);
            int? level = null;
            if (hasSword && !isLocked)
                level = TryReadLevel(context, LevelRois[index]);

            slots.Add(new(hasSword, isLocked, level));
            LoggerHelper.Info($"[新习合] 第 {index + 1} 位：有刀={hasSword}，上锁={isLocked}，乱舞={level?.ToString() ?? "未识别"}");
        }

        return slots;
    }

    private static int? TryReadLevel<T>(T context, int[] roi) where T : IMaaContext
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);
            using var image = context.GetImage();
            if (image != null)
            {
                var text = context.GetText(roi[0], roi[1], roi[2], roi[3], image);
                var match = Regex.Match(text ?? string.Empty, @"\d+");
                if (int.TryParse(match.Value, out var level))
                    return level;
            }

            if (attempt < 2)
                ActionParamHelper.SleepWithStopCheck(context, 200);
        }

        return null;
    }

    private static bool ContainsGray(Bitmap bitmap, int[] roi, byte lower, byte upper)
    {
        return ContainsColor(bitmap, roi, lower, lower, lower, 0, upper, upper, upper);
    }

    private static bool ContainsColor(Bitmap bitmap, int[] roi, byte red, byte green, byte blue, byte tolerance)
    {
        return ContainsColor(
            bitmap,
            roi,
            (byte)Math.Max(0, red - tolerance),
            (byte)Math.Max(0, green - tolerance),
            (byte)Math.Max(0, blue - tolerance),
            (byte)Math.Min(255, red + tolerance),
            (byte)Math.Min(255, green + tolerance),
            (byte)Math.Min(255, blue + tolerance));
    }

    private static bool ContainsColor(Bitmap bitmap, int[] roi, byte lowerR, byte lowerG, byte lowerB, byte upperR, byte upperG, byte upperB)
    {
        if (roi[0] < 0 || roi[1] < 0 || roi[2] <= 0 || roi[3] <= 0
            || roi[0] + roi[2] > bitmap.PixelSize.Width || roi[1] + roi[3] > bitmap.PixelSize.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(roi), "习合目标标记区域超出截图范围");
        }

        var bytes = new byte[roi[2] * roi[3] * 4];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(roi[0], roi[1], roi[2], roi[3]), handle.AddrOfPinnedObject(), bytes.Length, roi[2] * 4);
        }
        finally
        {
            handle.Free();
        }

        for (var index = 0; index < bytes.Length; index += 4)
        {
            var blue = bytes[index];
            var green = bytes[index + 1];
            var red = bytes[index + 2];
            if (red >= lowerR && red <= upperR && green >= lowerG && green <= upperG && blue >= lowerB && blue <= upperB)
                return true;
        }

        return false;
    }
}
