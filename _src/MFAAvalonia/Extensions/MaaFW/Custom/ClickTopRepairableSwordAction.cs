using Avalonia;
using Avalonia.Media.Imaging;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using System;
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

    /// <summary>最多滑动次数(可经 action_param 覆盖)</summary>
    public const int DefaultMaxSwipes = 8;

    /// <summary>长按滑动参数(可经 action_param 覆盖):按住起点 0.5s → 800ms 滑动到终点 → 再按住 1s</summary>
    public static readonly int[] DefaultSwipeFrom = [732, 638];
    public static readonly int[] DefaultSwipeTo = [732, 131];
    public const int DefaultPressHoldMs = 500;
    public const int DefaultSwipeDuration = 800;
    public const int DefaultReleaseHoldMs = 1000;

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
            // 三段式长按滑动:按住起点 0.5s → 800ms 滑动到终点 → 再按住 1s
            context.Swipe(swipeFrom[0], swipeFrom[1], swipeFrom[0], swipeFrom[1], pressHoldMs);
            context.Swipe(swipeFrom[0], swipeFrom[1], swipeTo[0], swipeTo[1], swipeDuration);
            context.Swipe(swipeTo[0], swipeTo[1], swipeTo[0], swipeTo[1], releaseHoldMs);
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

        // 从顶向下扫描:第一行含白色标记的像素,取该行白色像素的平均 x 作为点击目标
        int? hitY = null;
        var hitXSum = 0;
        var hitCount = 0;
        for (int y = 0; y < h && hitY == null; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = (y * w + x) * 4;
                var b = pixelBytes[idx];
                var g = pixelBytes[idx + 1];
                var r = pixelBytes[idx + 2];
                if (r >= lower[0] && r <= upper[0]
                    && g >= lower[1] && g <= upper[1]
                    && b >= lower[2] && b <= upper[2])
                {
                    hitY = y;
                    hitXSum += x;
                    hitCount++;
                }
            }
        }

        if (hitY == null || hitCount == 0)
            return null;

        return (x0 + hitXSum / hitCount, y0 + hitY.Value);
    }
}
