using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using MFAAvalonia.Services;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>读取仓库资源数量并写入自动识别草稿。</summary>
public sealed class WarehouseReadResourceAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(WarehouseReadResourceAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var json = ActionParamHelper.Parse(args.ActionParam);
        var resource = (string?)json["resource"];
        var roi = ParseRoi(json["roi"] as JArray);
        if (string.IsNullOrWhiteSpace(resource))
            throw new InvalidOperationException("仓库识别资源名称缺失");

        using var image = context.GetImage();
        if (image == null)
        {
            LoggerHelper.Warning($"[仓库识别] {resource} 无法获取截图，跳过本次写入");
            return true;
        }

        var text = context.GetText(roi[0], roi[1], roi[2], roi[3], image);
        LoggerHelper.Info($"[仓库识别] {resource} OCR 原文：{text}");
        if (!WarehouseScanDraftService.TryParseCount(text, out var value))
        {
            LoggerHelper.Warning($"[仓库识别] {resource} OCR 数值无效，保留已有草稿");
            return true;
        }

        WarehouseScanDraftService.UpdateCoreResource(DraftPath, resource, value);
        LoggerHelper.Info($"[仓库识别] {resource} 识别到：{value}");
        return true;
    }

    private static int[] ParseRoi(JArray? roi)
    {
        if (roi == null || roi.Count != 4)
            throw new InvalidOperationException("仓库识别 OCR ROI 必须是 [x, y, w, h]");
        return roi.ToObject<int[]>()!;
    }

    private static string DraftPath => Path.Combine(AppPaths.ConfigDirectory, "warehouse_scan.json");
}
