using Avalonia;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class SwordDropLogAction : IMaaCustomAction
{
    private static readonly string[] SwordTypes =
    [
        "大太刀", "短刀", "胁差", "打刀", "太刀", "薙刀", "枪", "剑"
    ];

    // 纯色校验缺省值:该区域全部像素命中目标色时判定为非刀剑掉落画面(如内番完成对话),
    // 跳过 OCR 与打点,但保留原点击行为
    private static readonly int[] DefaultCheckRoi = [53, 257, 54, 32];
    private static readonly int[] DefaultCheckColor = [248, 244, 230];
    private const int DefaultCheckTolerance = 3;

    public string Name { get; set; } = nameof(SwordDropLogAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var json = ActionParamHelper.Parse(args.ActionParam);
        var roi = ParseArray(json["roi"] as JArray, "刀剑掉落 OCR ROI");
        var click = ParseArray(json["click"] as JArray, "刀剑掉落点击区域");
        var task = (string?)json["task"] ?? "刀剑掉落";
        var prefix = $"[{task}]";

        if (IsPlainBackdrop(context, json))
        {
            // 说明性日志供排查,解析器只认词表行,该行不会计入统计
            LoggerHelper.Info($"{prefix} 非刀剑掉落画面，跳过 OCR 打点");
        }
        else
        {
            var text = ReadText(context, roi);
            if (TryValidateSword(text, out var swordType, out var swordName))
            {
                LoggerHelper.Info($"{prefix} 刀剑掉落 {swordType} {swordName}");
            }
        }

        ClickRectangle(context, click);
        return true;
    }

    /// <summary>
    /// 纯色校验:指定区域内全部像素命中目标色(含容差)时判定为非刀剑掉落画面,
    /// 跳过 OCR 与打点。参数 check_roi / check_color / check_tolerance 可覆盖缺省值。
    /// </summary>
    private static bool IsPlainBackdrop<T>(T context, JObject json) where T : IMaaContext
    {
        var checkRoi = json["check_roi"] is JArray checkArray
            ? ParseArray(checkArray, "刀剑掉落纯色校验 ROI")
            : DefaultCheckRoi;
        var checkColor = json["check_color"] is JArray colorArray
            ? ParseArray(colorArray, "刀剑掉落纯色校验目标色")
            : DefaultCheckColor;
        var tolerance = Math.Max(0, (int?)json["check_tolerance"] ?? DefaultCheckTolerance);

        using var image = context.GetImage();
        if (image == null)
            return false;

        using var bitmap = image.ToBitmap();
        if (bitmap == null)
            return false;

        int x0 = checkRoi[0], y0 = checkRoi[1], w = checkRoi[2], h = checkRoi[3];
        if (w <= 0 || h <= 0 || x0 < 0 || y0 < 0 || x0 + w > bitmap.PixelSize.Width || y0 + h > bitmap.PixelSize.Height)
        {
            LoggerHelper.Warning($"纯色校验 ROI 越界: roi=[{x0},{y0},{w},{h}], 截图={bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
            return false;
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

        var isPlain = true;
        for (var i = 0; i < pixelBytes.Length; i += 4)
        {
            var b = Math.Abs(pixelBytes[i] - checkColor[2]);
            var g = Math.Abs(pixelBytes[i + 1] - checkColor[1]);
            var r = Math.Abs(pixelBytes[i + 2] - checkColor[0]);
            if (b > tolerance || g > tolerance || r > tolerance)
            {
                isPlain = false;
                break;
            }
        }

        return isPlain;
    }

    private static bool TryValidateSword(
        string text,
        out string swordType,
        out string swordName)
    {
        swordType = string.Empty;
        swordName = string.Empty;

        var normalized = Regex.Replace(text ?? string.Empty, @"\s+", string.Empty);
        if (string.IsNullOrEmpty(normalized))
            return false;

        var type = SwordTypes.FirstOrDefault(normalized.StartsWith);
        if (type == null)
            return false;

        var name = normalized[type.Length..];
        if (name.Length < 2)
            return false;

        var map = FormationContext.LoadSwordTypeMap();
        if (!SwordNameMatcher.TryMatch(name, type, map, out var canonicalName))
            return false;

        swordType = type;
        swordName = canonicalName;
        return true;
    }

    private static string ReadText<T>(T context, int[] roi) where T : IMaaContext
    {
        using var image = context.GetImage();
        if (image == null)
            return string.Empty;

        var taskModel = new MaaNode
        {
            Name = "SwordDropOCR",
            Recognition = "OCR",
            Roi = roi,
        };
        var detail = context.RunRecognition(taskModel, image);
        if (detail?.Detail == null)
            return string.Empty;

        var query = JsonConvert.DeserializeObject<MaaExtensions.RecognitionQuery>(detail.Detail);
        return query?.Best?.Text ?? string.Empty;
    }

    private static void ClickRectangle<T>(T context, int[] rectangle) where T : IMaaContext
    {
        var x = rectangle[0] + (rectangle[2] > 0 ? Random.Shared.Next(rectangle[2]) : 0);
        var y = rectangle[1] + (rectangle[3] > 0 ? Random.Shared.Next(rectangle[3]) : 0);
        context.Click(x, y);
    }

    private static int[] ParseArray(JArray? value, string name)
    {
        if (value == null || value.Count != 4)
            throw new Exception($"{name}必须是 [x, y, w, h]");
        return value.ToObject<int[]>()!;
    }
}
