using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using MFAAvalonia.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>扫描所持道具页面，并通过上滑遍历所有物品卡片。</summary>
public sealed class WarehouseScanItemsAction : IMaaCustomAction
{
    private const int MaxPages = 100;
    private const int SamePageLimit = 3;
    private const int Contact = 0;
    private const int StartX = 656;
    private const int StartY = 567;
    private const int EndX = 656;
    private const int EndY = 177;
    private const int PressBeforeMoveMilliseconds = 500;
    private const int MoveMilliseconds = 500;
    private const int PressAfterMoveMilliseconds = 500;
    private const int MoveSteps = 10;

    private static readonly int[][] DefaultNameRois =
    [
        [64, 143, 540, 36], [655, 143, 540, 36],
        [64, 336, 540, 36], [655, 336, 540, 36],
        [64, 529, 540, 36], [655, 529, 540, 36],
    ];

    private static readonly int[][] DefaultCountRois =
    [
        [170, 275, 100, 43], [760, 275, 100, 43],
        [170, 468, 100, 43], [760, 468, 100, 43],
        [170, 661, 100, 43], [760, 661, 100, 43],
    ];
    private static readonly int[] DefaultKobanRoi = [977, 32, 189, 48];

    public string Name { get; set; } = nameof(WarehouseScanItemsAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var json = ActionParamHelper.Parse(args.ActionParam);
            var nameRois = ParseRois(json["name_rois"] as JArray, DefaultNameRois);
            var countRois = ParseRois(json["count_rois"] as JArray, DefaultCountRois);
            var kobanRoi = ParseRoi(json["koban_roi"] as JArray, DefaultKobanRoi);
            var draftPath = Path.Combine(AppPaths.ConfigDirectory, "warehouse_scan.json");
            ReadKoban(context, kobanRoi, draftPath);
            var previousSignature = string.Empty;
            var unchangedPages = 0;

            for (var page = 0; page < MaxPages; page++)
            {
                ActionParamHelper.ThrowIfStopping(context);
                var visibleItems = ReadVisibleItems(context, nameRois, countRois);
                foreach (var item in visibleItems)
                {
                    WarehouseScanDraftService.UpdateOtherItem(draftPath, item.Name, item.Count);
                    LoggerHelper.Info($"[仓库识别] 其他物品 {item.Name}：{item.Count}");
                }

                var signature = string.Join("|", visibleItems.Select(item => $"{item.Name}={item.Count}"));
                if (string.Equals(signature, previousSignature, StringComparison.Ordinal))
                    unchangedPages++;
                else
                    unchangedPages = 0;

                LoggerHelper.Info($"[仓库识别] 所持道具页面扫描：第 {page + 1} 页，识别 {visibleItems.Count} 项，连续未变化 {unchangedPages} 次");
                if (unchangedPages >= SamePageLimit)
                {
                    var completedDraft = WarehouseScanDraftService.Load(draftPath);
                    WarehouseScanDraftService.AppendSnapshot(draftPath, completedDraft.CoreResources);
                    LoggerHelper.Info("[仓库识别] 所持道具扫描完成，已追加核心资源历史快照");
                    return true;
                }

                previousSignature = signature;
                ScrollUp(context);
                ActionParamHelper.SleepWithStopCheck(context, 300);
            }

            LoggerHelper.Warning($"[仓库识别] 所持道具页面扫描超过 {MaxPages} 页，已停止");
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[仓库识别] 所持道具扫描已停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[仓库识别] 所持道具扫描失败：{e.Message}");
            return false;
        }
    }

    private static List<VisibleItem> ReadVisibleItems<T>(T context, int[][] nameRois, int[][] countRois)
        where T : IMaaContext
    {
        using var image = context.GetImage();
        if (image == null)
            throw new InvalidOperationException("无法获取所持道具页面截图");

        var items = new List<VisibleItem>();
        for (var i = 0; i < nameRois.Length; i++)
        {
            var rawName = context.GetText(nameRois[i][0], nameRois[i][1], nameRois[i][2], nameRois[i][3], image)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Trim();
            if (string.IsNullOrWhiteSpace(rawName))
                continue;

            var name = WarehouseScanDraftService.NormalizeOtherItemName(rawName);
            if (!string.Equals(rawName, name, StringComparison.Ordinal))
                LoggerHelper.Info($"[仓库识别] 物品名称纠正：{rawName} → {name}");

            var countText = context.GetText(countRois[i][0], countRois[i][1], countRois[i][2], countRois[i][3], image);
            LoggerHelper.Info($"[仓库识别] {name} 数量 OCR 原文：{countText}");
            if (WarehouseScanDraftService.TryParseCount(countText, out var count))
                items.Add(new VisibleItem(name, count));
        }

        return items;
    }

    private static void ReadKoban<T>(T context, int[] roi, string draftPath) where T : IMaaContext
    {
        using var image = context.GetImage();
        if (image == null)
            throw new InvalidOperationException("无法获取小判页面截图");

        var text = context.GetText(roi[0], roi[1], roi[2], roi[3], image);
        LoggerHelper.Info($"[仓库识别] 小判 OCR 原文：{text}");
        if (!WarehouseScanDraftService.TryParseCount(text, out var value))
        {
            LoggerHelper.Warning("[仓库识别] 小判 OCR 数值无效，保留已有草稿");
            return;
        }

        WarehouseScanDraftService.UpdateCoreResource(draftPath, "小判", value);
        LoggerHelper.Info($"[仓库识别] 小判识别到：{value}");
    }

    private static void ScrollUp<T>(T context) where T : IMaaContext
    {
        var tasker = context.Tasker;
        tasker.TouchDown(Contact, StartX, StartY, 1);
        ActionParamHelper.SleepWithStopCheck(context, PressBeforeMoveMilliseconds);

        for (var step = 1; step <= MoveSteps; step++)
        {
            ActionParamHelper.ThrowIfStopping(context);
            var x = StartX + (EndX - StartX) * step / MoveSteps;
            var y = StartY + (EndY - StartY) * step / MoveSteps;
            tasker.TouchMove(Contact, x, y, 1);
            ActionParamHelper.SleepWithStopCheck(context, MoveMilliseconds / MoveSteps);
        }

        ActionParamHelper.SleepWithStopCheck(context, PressAfterMoveMilliseconds);
        tasker.TouchUp(Contact);
    }

    private static int[][] ParseRois(JArray? value, int[][] fallback)
    {
        if (value == null)
            return fallback;

        var rois = value.ToObject<int[][]>();
        if (rois == null || rois.Length != fallback.Length || rois.Any(roi => roi.Length != 4))
            throw new InvalidOperationException("所持道具 OCR ROI 必须包含六组 [x, y, w, h]");
        return rois;
    }

    private static int[] ParseRoi(JArray? value, int[] fallback)
    {
        if (value == null)
            return fallback;
        var roi = value.ToObject<int[]>();
        if (roi == null || roi.Length != 4)
            throw new InvalidOperationException("仓库 OCR ROI 必须是 [x, y, w, h]");
        return roi;
    }

    private readonly record struct VisibleItem(string Name, int Count);
}
