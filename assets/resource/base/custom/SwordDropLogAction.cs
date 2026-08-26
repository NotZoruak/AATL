using Avalonia;
using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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
    private static readonly int[] AnimationRoi = [131, 354, 136, 126];
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
            var animationKind = SwordDropNotificationMatcher.GetAnimationKind(ReadText(context, AnimationRoi));
            if (animationKind is SwordDropAnimationKind.Specialization or SwordDropAnimationKind.Kiwame)
            {
                LoggerHelper.Info($"{prefix} 特化或极化动画，跳过刀剑掉落识别");
            }
            else if (animationKind == SwordDropAnimationKind.InitialDrop)
            {
                var text = ReadText(context, roi);
                if (TryValidateSword(text, out var swordType, out var swordName))
                {
                    SaveScreenshot(context, swordName, "初始掉落");
                    LoggerHelper.Info($"{prefix} 刀剑掉落 {swordType} {swordName}");
                    TryNotify(swordName, swordType);
                }
                else
                {
                    SaveScreenshot(context, "未识别刀剑", "初始掉落");
                    LoggerHelper.Warning($"{prefix} 初始掉落刀名 OCR 校验失败: {text}");
                }
            }
            else
            {
                ProcessOrdinaryDrop(context, roi, prefix);
            }
        }

        ClickRectangle(context, click);
        return true;
    }

    /// <summary>处理未识别到结果动画标记时的原有掉落识别流程。</summary>
    private static void ProcessOrdinaryDrop<T>(T context, int[] roi, string prefix) where T : IMaaContext
    {
        var text = ReadText(context, roi);
        if (TryValidateSword(text, out var swordType, out var swordName))
        {
            LoggerHelper.Info($"{prefix} 刀剑掉落 {swordType} {swordName}");
            TryNotify(swordName, swordType);
        }
    }

    /// <summary>按全局开关和播报名单发送刀剑掉落桌面通知。</summary>
    private static void TryNotify(string swordName, string swordType)
    {
        if (!ConfigurationManager.Current.GetValue(ConfigurationKeys.SwordDropNotificationEnabled, false))
            return;

        if (!ConfigurationManager.Current.TryGetValue(
                ConfigurationKeys.SwordDropNotificationSwords, out List<string>? swords)
            || !SwordDropNotificationMatcher.ShouldNotify(true, swords, swordName))
        {
            return;
        }

        ToastNotification.Show(SwordDropNotificationMatcher.FormatMessage(swordType, swordName));
    }

    /// <summary>保存当前完整画面，截图失败不影响后续点击。</summary>
    private static void SaveScreenshot<T>(T context, string swordName, string suffix) where T : IMaaContext
    {
        try
        {
            using var image = context.GetImage();
            if (image == null)
                return;

            using var bitmap = image.ToBitmap();
            if (bitmap == null)
                return;

            var directory = Path.Combine(AppPaths.InstallRoot, "debug", "sword_drop");
            Directory.CreateDirectory(directory);
            var safeName = string.Concat(swordName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            var path = Path.Combine(directory, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{safeName}_{suffix}.png");
            bitmap.Save(path);
            LoggerHelper.Info($"保存刀剑掉落截图: {path}");
        }
        catch (Exception e)
        {
            LoggerHelper.Warning($"保存刀剑掉落截图失败: {e.Message}");
        }
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

    /// <summary>
    /// 为刀剑掉落 OCR 提供受控的刀名相近字形匹配。
    /// </summary>
    private static class SwordNameMatcher
    {
        private static readonly string[] SimilarCharacterGroups =
        [
            "掘堀",
            "広广厂",
            "國国",
        ];

        public static bool TryMatch(
            string recognizedName,
            string expectedType,
            IReadOnlyDictionary<string, string> swordTypeMap,
            out string canonicalName)
        {
            canonicalName = string.Empty;
            var normalized = Regex.Replace(recognizedName ?? string.Empty, @"\s+", string.Empty);
            if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(expectedType))
                return false;

            if (swordTypeMap.TryGetValue(normalized, out var exactType)
                && string.Equals(exactType, expectedType, StringComparison.Ordinal))
            {
                canonicalName = normalized;
                return true;
            }

            var candidates = swordTypeMap
                .Where(pair => string.Equals(pair.Value, expectedType, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .Where(candidate => IsSimilarName(normalized, candidate))
                .ToList();

            if (candidates.Count != 1)
                return false;

            canonicalName = candidates[0];
            return true;
        }

        private static bool IsSimilarName(string recognizedName, string candidate)
        {
            if (recognizedName.Length != candidate.Length)
                return false;

            var differences = 0;
            for (var i = 0; i < recognizedName.Length; i++)
            {
                if (recognizedName[i] == candidate[i])
                    continue;

                if (!IsSimilarCharacter(recognizedName[i], candidate[i]))
                    return false;

                differences++;
                if (differences > 1)
                    return false;
            }

            return differences == 1;
        }

        private static bool IsSimilarCharacter(char left, char right) =>
            SimilarCharacterGroups.Any(group => group.Contains(left) && group.Contains(right));
    }
}
