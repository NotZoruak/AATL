using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class ResourcePointLogAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(ResourcePointLogAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var json = ActionParamHelper.Parse(args.ActionParam);
        var roi = ParseRoi(json["roi"] as JArray ?? throw new System.Exception("资源点 OCR ROI 缺失"));
        var expected = (string?)json["expected"] ?? "获得";
        var timeout = Math.Max(1000, (int?)json["timeout"] ?? 10000);
        var pollInterval = Math.Max(50, (int?)json["poll_interval"] ?? 200);
        // task 参数决定打点前缀（合战场/地下城）；缺省回退旧前缀 [资源点]
        var task = (string?)json["task"] ?? string.Empty;
        var prefix = string.IsNullOrWhiteSpace(task) ? "[资源点]" : $"[{task}]";
        var startTime = System.DateTime.UtcNow;

        var text = ReadText(context, roi);
        LoggerHelper.Info($"[资源点] OCR 识别结果: {text}");
        LogGained(prefix, text);

        while ((System.DateTime.UtcNow - startTime).TotalMilliseconds < timeout)
        {
            ActionParamHelper.ThrowIfStopping(context);
            ActionParamHelper.SleepWithStopCheck(context, pollInterval);

            text = ReadText(context, roi);
            if (!text.Replace(" ", string.Empty).Contains(expected, System.StringComparison.Ordinal))
            {
                LoggerHelper.Info($"[资源点] OCR 已无法识别“{expected}”，结束等待");
                return true;
            }
        }

        LoggerHelper.Warning($"[资源点] 等待 OCR 消失超时（{timeout}ms），当前结果: {text}");
        return true;
    }

    // 把 OCR 文本（如「获得木炭×20」）转成打点格式「木炭x20」，多个资源以空格分隔
    private static void LogGained(string prefix, string text)
    {
        var matches = Regex.Matches(text, @"获得\s*(?<name>[^×\s]+)×(?<count>\d+)");
        if (matches.Count == 0)
            return;

        var parts = new List<string>();
        foreach (Match match in matches)
            parts.Add($"{match.Groups["name"].Value}x{match.Groups["count"].Value}");
        LoggerHelper.Info($"{prefix} 资源点获取 {string.Join(" ", parts)}");
    }

    private static string ReadText<T>(T context, int[] roi) where T : IMaaContext
    {
        using var image = context.GetImage();
        if (image == null)
            return string.Empty;

        var taskModel = new MaaNode
        {
            Name = "ResourcePointOCR",
            Recognition = "OCR",
            Roi = roi
        };
        var detail = context.RunRecognition(taskModel, image);
        if (detail == null || string.IsNullOrWhiteSpace(detail.Detail))
            return string.Empty;

        var query = JsonConvert.DeserializeObject<MaaExtensions.RecognitionQuery>(detail.Detail);
        return query?.Best?.Text ?? string.Empty;
    }

    private static int[] ParseRoi(JArray roi)
    {
        if (roi.Count != 4)
            throw new System.Exception("资源点 OCR ROI 必须是 [x, y, w, h]");
        return roi.ToObject<int[]>()!;
    }
}
