using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 小判箱掉落打点：记日志一次后轮询等待图标消失。
/// 小判箱弹窗动画期间（约 10 秒）模板会持续命中，且点击无法关闭弹窗，
/// 因此不在节点层重复识别，而是在本 action 内等动画播完再返回主循环，
/// 避免一次掉落被重复打点。
/// </summary>
public class KobanChestLogAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(KobanChestLogAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var json = ActionParamHelper.Parse(args.ActionParam);
        var roi = ParseRoi(json["roi"] as JArray ?? throw new Exception("小判箱模板 ROI 缺失"));
        var template = (string?)json["template"] ?? throw new Exception("小判箱模板路径缺失");
        var threshold = (double?)json["threshold"] ?? 0.8;
        var greenMask = (bool?)json["green_mask"] ?? true;
        // 等待图标消失的总超时（动画实测约 10 秒，兜底放宽到 15 秒）
        var timeout = Math.Max(1000, (int?)json["timeout"] ?? 15000);
        var pollInterval = Math.Max(50, (int?)json["poll_interval"] ?? 200);
        // 连续未命中累计时长达到该值判定图标已消失（需大于动画间隙，实测间隙约 2.5 秒）
        var settleMs = Math.Max(500, (int?)json["settle_ms"] ?? 4000);
        // task 参数决定打点前缀（合战场/地下城）；缺省回退旧前缀 [小判箱]
        var task = (string?)json["task"] ?? string.Empty;
        var prefix = string.IsNullOrWhiteSpace(task) ? "[小判箱]" : $"[{task}]";

        LoggerHelper.Info($"{prefix} 小判箱掉落");

        var startTime = DateTime.UtcNow;
        // 连续未命中的起始时间；命中时重置，用于区分动画间隙与真正的弹窗结束
        var missStart = DateTime.MinValue;

        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeout)
        {
            ActionParamHelper.ThrowIfStopping(context);
            ActionParamHelper.SleepWithStopCheck(context, pollInterval);

            using var image = context.GetImage();
            if (image == null)
                continue;

            if (IsKobanChestHit(context, image, roi, template, threshold, greenMask))
            {
                missStart = DateTime.MinValue;
                continue;
            }

            if (missStart == DateTime.MinValue)
                missStart = DateTime.UtcNow;
            else if ((DateTime.UtcNow - missStart).TotalMilliseconds >= settleMs)
            {
                LoggerHelper.Info($"{prefix} 小判箱图标已消失，结束等待");
                return true;
            }
        }

        LoggerHelper.Warning($"{prefix} 等待小判箱图标消失超时（{timeout}ms）");
        return true;
    }

    /// <summary>在指定 ROI 内做一次模板匹配，返回是否命中（score ≥ 阈值）</summary>
    private static bool IsKobanChestHit<T>(T context, IMaaImageBuffer image, int[] roi, string template,
        double threshold, bool greenMask) where T : IMaaContext
    {
        var taskModel = new MaaNode
        {
            Name = "KobanChestCheck",
            Recognition = "TemplateMatch",
            Roi = new[] { roi[0], roi[1], roi[2], roi[3] },
            Template = [template],
            Threshold = new[] { threshold },
            GreenMask = greenMask,
        };
        var detail = context.RunRecognition(taskModel, image);
        if (detail?.Detail == null)
            return false;
        var query = JsonConvert.DeserializeObject<MaaExtensions.RecognitionQuery>(detail.Detail);
        return query?.Best?.Score is { } score && score >= threshold;
    }

    private static int[] ParseRoi(JArray roi)
    {
        if (roi.Count != 4)
            throw new Exception("小判箱模板 ROI 必须是 [x, y, w, h]");
        return roi.ToObject<int[]>()!;
    }
}
