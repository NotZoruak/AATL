using Avalonia;
using Avalonia.Media.Imaging;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using System;
using System.IO;
using System.Threading;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 点击修复界面最上方的可修复刀剑:
/// 在指定 ROI 内扫描白色标记像素(可修复刀剑的标记),取最上方白色区域的中心点击。
/// 当前视野内未找到时按滑动参数上滑列表继续查找,最多滑动 max_swipes 次。
/// 用于修复工坊选刀时避开出阵任务中使用的刀剑(出阵中的刀剑无白色标记)。
/// </summary>
public class ClickTopRepairableSwordAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(ClickTopRepairableSwordAction);

    /// <summary>扫描 ROI(可经 action_param 覆盖):修复界面刀剑列表左侧标记列</summary>
    public static readonly int[] DefaultRoi = [459, 127, 16, 554];

    /// <summary>白色标记像素下限/上限(可经 action_param 覆盖),默认 [252,252,252]~[255,255,255]</summary>
    public static readonly byte[] DefaultLower = [252, 252, 252];
    public static readonly byte[] DefaultUpper = [255, 255, 255];

    /// <summary>可接受白色标记的最小连续矩形尺寸</summary>
    public const int MinWhiteBlockWidth = 15;
    public const int MinWhiteBlockHeight = 7;

    /// <summary>最多滑动次数(可经 action_param 覆盖)</summary>
    public const int DefaultMaxSwipes = 8;

    /// <summary>长按滑动参数(可经 action_param 覆盖):按住起点 0.5s → 800ms 滑动到终点 → 再按住 1s</summary>
    public static readonly int[] DefaultSwipeFrom = [732, 638];
    public static readonly int[] DefaultSwipeTo = [732, 131];
    public const int DefaultPressHoldMs = 500;
    public const int DefaultSwipeDuration = 800;
    public const int DefaultReleaseHoldMs = 1000;
    public const int DefaultSwipeSteps = 20;

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        ActionParamHelper.ThrowIfStopping(context);
        var json = ActionParamHelper.Parse(args.ActionParam);
        var roi = json?["roi"]?.ToObject<int[]>() ?? DefaultRoi;
        var lower = json?["lower"]?.ToObject<byte[]>() ?? DefaultLower;
        var upper = json?["upper"]?.ToObject<byte[]>() ?? DefaultUpper;
        var maxSwipes = json?["max_swipes"]?.ToObject<int>() ?? DefaultMaxSwipes;
        var swipeFrom = json?["swipe_from"]?.ToObject<int[]>() ?? DefaultSwipeFrom;
        var swipeTo = json?["swipe_to"]?.ToObject<int[]>() ?? DefaultSwipeTo;
        var pressHoldMs = json?["press_hold_ms"]?.ToObject<int>() ?? DefaultPressHoldMs;
        var swipeDuration = json?["swipe_duration"]?.ToObject<int>() ?? DefaultSwipeDuration;
        var releaseHoldMs = json?["release_hold_ms"]?.ToObject<int>() ?? DefaultReleaseHoldMs;

        for (int attempt = 0; attempt <= maxSwipes; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);

            var hit = ScanAndClick(context, roi, lower, upper);
            if (hit != null)
            {
                LoggerHelper.Info($"[修刀选刀] 找到可修复刀剑(第 {attempt} 屏),点击 ({hit.Value.X},{hit.Value.Y})");
                context.Click(hit.Value.X, hit.Value.Y);
                return true;
            }

            if (attempt >= maxSwipes)
                break;

            LoggerHelper.Info($"[修刀选刀] 当前视野未找到可修复刀剑,长按滑动列表({attempt + 1}/{maxSwipes})");
            // 在同一个触摸会话内完成长按、上滑和终点保持，避免中途松手被识别为点击
            var touchActive = false;
            try
            {
                context.TouchDown(0, swipeFrom[0], swipeFrom[1], 1);
                touchActive = true;
                Thread.Sleep(pressHoldMs);

                var stepDelay = Math.Max(1, swipeDuration / DefaultSwipeSteps);
                for (var step = 1; step <= DefaultSwipeSteps; step++)
                {
                    var currentX = swipeFrom[0] + (swipeTo[0] - swipeFrom[0]) * step / DefaultSwipeSteps;
                    var currentY = swipeFrom[1] + (swipeTo[1] - swipeFrom[1]) * step / DefaultSwipeSteps;
                    context.TouchMove(0, currentX, currentY, 1);
                    Thread.Sleep(stepDelay);
                }

                Thread.Sleep(releaseHoldMs);
            }
            finally
            {
                if (touchActive)
                    context.TouchUp(0);
            }
            Thread.Sleep(300);
        }

        LoggerHelper.Warning("[修刀选刀] 滑动后仍未找到可修复刀剑");
        return false;
    }

    /// <summary>
    /// 扫描截图 ROI 内白色标记,返回最上方白色区域的点击坐标;未找到返回 null。
    /// </summary>
    private static (int X, int Y)? ScanAndClick<T>(T context, int[] roi, byte[] lower, byte[] upper) where T : IMaaContext
    {
        using var image = context.GetImage();
        if (image == null)
        {
            LoggerHelper.Warning("[修刀选刀] 获取截图失败");
            return null;
        }

        using var bitmap = image.ToBitmap();
        if (bitmap == null)
        {
            LoggerHelper.Warning("[修刀选刀] 截图转 Bitmap 失败");
            return null;
        }

        var diagnosticDirectory = Path.Combine(AppPaths.InstallRoot, "debug", "repair_selection");
        Directory.CreateDirectory(diagnosticDirectory);
        var diagnosticPath = Path.Combine(diagnosticDirectory, $"repair_selection_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        bitmap.Save(diagnosticPath);
        LoggerHelper.Info($"[修刀选刀] 诊断截图={diagnosticPath}, PixelFormat={bitmap.Format}, AlphaFormat={bitmap.AlphaFormat}");

        int x0 = roi[0], y0 = roi[1], w = roi[2], h = roi[3];
        if (w <= 0 || h <= 0 || x0 < 0 || y0 < 0 || x0 + w > bitmap.PixelSize.Width || y0 + h > bitmap.PixelSize.Height)
        {
            LoggerHelper.Warning($"[修刀选刀] ROI 越界: roi=[{x0},{y0},{w},{h}], 截图={bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
            return null;
        }

        // 读取 ROI 区域像素(BGRA)
        var pixelBytes = new byte[w * h * 4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixelBytes, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(x0, y0, w, h), handle.AddrOfPinnedObject(), pixelBytes.Length, w * 4);
        }
        finally
        {
            handle.Free();
        }

        var hit = FindSolidWhiteBlock(
            pixelBytes,
            w,
            h,
            lower,
            upper,
            MinWhiteBlockWidth,
            MinWhiteBlockHeight);
        if (hit == null)
            return null;

        var hitIndex = (hit.Value.Y * w + hit.Value.X) * 4;
        LoggerHelper.Info(
            $"[修刀选刀] 命中原始数据: ROI坐标=({hit.Value.X},{hit.Value.Y}), " +
            $"屏幕坐标=({x0 + hit.Value.X},{y0 + hit.Value.Y}), " +
            $"连续白色区域={MinWhiteBlockWidth}×{MinWhiteBlockHeight}, " +
            $"bytes=[{pixelBytes[hitIndex]},{pixelBytes[hitIndex + 1]},{pixelBytes[hitIndex + 2]},{pixelBytes[hitIndex + 3]}]");

        return (x0 + hit.Value.X, y0 + hit.Value.Y);
    }

    /// <summary>
    /// 从顶向下查找完整的连续白色矩形，并返回矩形中心；找不到时返回 null。
    /// </summary>
    public static (int X, int Y)? FindSolidWhiteBlock(
        byte[] pixelBytes,
        int width,
        int height,
        byte[] lower,
        byte[] upper,
        int blockWidth,
        int blockHeight)
    {
        if (width <= 0 || height <= 0 || blockWidth <= 0 || blockHeight <= 0
            || blockWidth > width || blockHeight > height
            || pixelBytes.Length < width * height * 4
            || lower.Length < 3 || upper.Length < 3)
            return null;

        for (var top = 0; top <= height - blockHeight; top++)
        {
            for (var left = 0; left <= width - blockWidth; left++)
            {
                var isSolidWhite = true;
                for (var y = top; y < top + blockHeight && isSolidWhite; y++)
                {
                    for (var x = left; x < left + blockWidth; x++)
                    {
                        var index = (y * width + x) * 4;
                        var b = pixelBytes[index];
                        var g = pixelBytes[index + 1];
                        var r = pixelBytes[index + 2];
                        if (r < lower[0] || r > upper[0]
                            || g < lower[1] || g > upper[1]
                            || b < lower[2] || b > upper[2])
                        {
                            isSolidWhite = false;
                            break;
                        }
                    }
                }

                if (isSolidWhite)
                    return (left + blockWidth / 2, top + blockHeight / 2);
            }
        }

        return null;
    }
}
