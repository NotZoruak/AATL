using Avalonia;
using Avalonia.Media.Imaging;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>按乱舞等级尽量减少习合素材溢出的正常习合选择动作。</summary>
public sealed class MixGreedySelectionAction : IMaaCustomAction
{
    private static readonly int[] LevelRoi = [410, 299, 31, 27];
    private static readonly int[] ClearSelection = [1154, 363, 95, 26];
    private static readonly int[] SelectAll = [1158, 463, 80, 36];
    private static readonly int[][] MaterialPositions =
    [
        [1056, 143, 27, 55],
        [1054, 243, 27, 54],
        [1052, 342, 33, 61],
        [1050, 439, 37, 65],
        [1053, 545, 28, 59],
        [1052, 648, 28, 26]
    ];

    private const int BackX = 152;
    private const int BackY = 23;
    private const int PinkX = 287;
    private const int PinkY = 309;
    private const int PinkR = 211;
    private const int PinkG = 69;
    private const int PinkB = 105;
    private const int ColorTolerance = 3;

    public string Name { get; set; } = nameof(MixGreedySelectionAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            if (!TryReadLevel(context, out var level))
            {
                LoggerHelper.Warning("[日课 合成] 葛朗台模式无法识别当前乱舞等级");
                return false;
            }

            LoggerHelper.Info($"[日课 合成] 葛朗台模式当前乱舞等级：{level}");
            if (level >= 7)
            {
                LoggerHelper.Info("[日课 合成] 当前乱舞等级已达到7级，返回刀剑列表");
                context.Click(BackX, BackY);
                return false;
            }

            ClickRegion(context, SelectAll, "一键选择");
            if (!TryReadLevel(context, out level))
            {
                LoggerHelper.Warning("[日课 合成] 一键选择后无法识别乱舞等级");
                return false;
            }

            LoggerHelper.Info($"[日课 合成] 一键选择后乱舞等级：{level}");
            if (level > 7)
            {
                LoggerHelper.Info("[日课 合成] 一键选择造成等级溢出，清空选择并逐个选择素材");
                ClearAndSelectUntilSeven(context);
                return true;
            }

            if (level == 7 && IsPinkProgress(context))
            {
                LoggerHelper.Info("[日课 合成] 一键选择造成经验溢出，清空选择并逐个选择素材");
                ClearAndSelectUntilSeven(context);
            }

            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[日课 合成] 手动停止葛朗台选择");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[日课 合成] 葛朗台选择异常：{e.Message}");
            return false;
        }
    }

    /// <summary>清空一键选择结果，再按顺序逐把选择，达到7级后停止。</summary>
    private static void ClearAndSelectUntilSeven<T>(T context) where T : IMaaContext
    {
        ClickRegion(context, ClearSelection, "取消一键选择");

        foreach (var position in MaterialPositions)
        {
            ActionParamHelper.ThrowIfStopping(context);
            ClickRegion(context, position, "逐个选择素材");

            if (!TryReadLevel(context, out var level))
            {
                LoggerHelper.Warning("[日课 合成] 逐个选择后无法识别乱舞等级，停止继续选择");
                return;
            }

            LoggerHelper.Info($"[日课 合成] 逐个选择后乱舞等级：{level}");
            if (level >= 7)
                return;
        }
    }

    /// <summary>点击指定区域中心，并等待画面完成一次更新。</summary>
    private static void ClickRegion<T>(T context, int[] region, string description) where T : IMaaContext
    {
        context.Click(region[0] + region[2] / 2, region[1] + region[3] / 2);
        LoggerHelper.Info($"[日课 合成] {description}：({region[0] + region[2] / 2},{region[1] + region[3] / 2})");
        ActionParamHelper.SleepWithStopCheck(context, 200);
    }

    /// <summary>读取当前具体习合页面左侧的乱舞等级。</summary>
    private static bool TryReadLevel<T>(T context, out int level) where T : IMaaContext
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            ActionParamHelper.ThrowIfStopping(context);
            using var image = context.GetImage();
            if (image != null)
            {
                var text = context.GetText(LevelRoi[0], LevelRoi[1], LevelRoi[2], LevelRoi[3], image);
                var match = Regex.Match(text ?? string.Empty, @"\d+");
                if (int.TryParse(match.Value, out level))
                    return true;
            }

            if (attempt < 4)
                ActionParamHelper.SleepWithStopCheck(context, 200);
        }

        level = -1;
        return false;
    }

    /// <summary>判断经验进度条是否仍有未消化的溢出经验。</summary>
    private static bool IsPinkProgress<T>(T context) where T : IMaaContext
    {
        using var image = context.GetImage();
        if (image == null)
            return false;

        using var bitmap = image.ToBitmap();
        if (bitmap == null)
            return false;

        var pixel = ReadPixel(bitmap, PinkX, PinkY);
        var hit = Math.Abs(pixel.R - PinkR) <= ColorTolerance
            && Math.Abs(pixel.G - PinkG) <= ColorTolerance
            && Math.Abs(pixel.B - PinkB) <= ColorTolerance;
        LoggerHelper.Info($"[日课 合成] 7级经验条溢出颜色识别：{hit}");
        return hit;
    }

    /// <summary>读取指定坐标的 RGB 像素。</summary>
    private static RgbColor ReadPixel(Bitmap bitmap, int x, int y)
    {
        if (x < 0 || y < 0 || x >= bitmap.PixelSize.Width || y >= bitmap.PixelSize.Height)
            return default;

        var bytes = new byte[4];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(x, y, 1, 1), handle.AddrOfPinnedObject(), bytes.Length, 4);
        }
        finally
        {
            handle.Free();
        }

        return new RgbColor(bytes[2], bytes[1], bytes[0]);
    }

    private readonly record struct RgbColor(byte R, byte G, byte B);
}
