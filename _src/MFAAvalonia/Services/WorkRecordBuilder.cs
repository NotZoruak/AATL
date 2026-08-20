using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MFAAvalonia.Models;

namespace MFAAvalonia.Services;

/// <summary>把日志事件流聚合为运行记录列表</summary>
public static class WorkRecordBuilder
{
    // 任务定义行：名称=[地下城] 入口=[Underground]（队列启动时批量打印，不代表任务开始）
    private static readonly Regex TaskDefRegex = new(
        @"名称=\[([^\]]+)\]\s+入口=\[([^\]]+)\]", RegexOptions.Compiled);

    // 任务轮次开始（Monitor 行）：开始任务：地下城——真正的任务执行起点
    private static readonly Regex StartTaskRegex = new(
        @"开始任务：([^\s：]+)", RegexOptions.Compiled);

    // 队列结束：停止前状态：SUCCEEDED（可能连续多条，取最后一条为准）
    private static readonly Regex StopStatusRegex = new(
        @"停止前状态：([A-Z_]+)", RegexOptions.Compiled);

    // 词表行：[地下城] 出阵 / [后勤] 派遣远征 部队3已派遣至 4-3
    // 内容行开头可能有 [cfg=Default][src=Monitor] 等上下文块,先跳过(key=value 带等号),再捕获 [前缀] 行为词
    // 前缀排除 = 与空白,避免 [cfg=Default] 等上下文块被误捕获为前缀(如系统 Warning 日志会显示进特殊情况)
    private static readonly Regex WordRegex = new(
        @"^(?:\[[a-zA-Z]+\s*=[^\]]*\]\s*)*\[([^\]\s=]+)\]\s+(\S+)(?:\s+(.*))?$", RegexOptions.Compiled);

    /// <summary>状态码 → 中文（NOT_STARTED 语义见 Build 中按是否执行过区分）</summary>
    public static readonly Dictionary<string, string> StatusMap = new()
    {
        ["SUCCEEDED"] = "成功",
        ["STOPPED"] = "手动停止",
        ["FAILED"] = "失败",
        ["NOT_STARTED"] = "未开始",
    };

    /// <summary>把事件流聚合为运行记录列表</summary>
    public static List<WorkRecord> Build(IEnumerable<LogEntry> entries)
    {
        var records = new List<WorkRecord>();
        var pending = new Queue<(string Name, string Entry)>(); // 已定义待执行的任务
        WorkRecord? current = null;
        var lastTime = DateTime.MinValue;
        // 短时间重复过滤：记录内最近一次词表行内容与时间
        var lastSeen = new Dictionary<WorkRecord, (DateTime Time, string Content)>();

        foreach (var entry in entries)
        {
            if (entry.Timestamp is null)
                continue;
            lastTime = entry.Timestamp.Value;

            // 1. 任务定义行：入待执行队列（批量打印，任务实际开始看「开始任务」行）
            var def = TaskDefRegex.Match(entry.Content);
            if (def.Success)
            {
                pending.Enqueue((def.Groups[1].Value, def.Groups[2].Value));
                continue;
            }

            // 2. 任务轮次开始：开新记录（同名任务连跑多轮只算一条）
            var start = StartTaskRegex.Match(entry.Content);
            if (start.Success)
            {
                var name = start.Groups[1].Value;
                if (current != null && current.TaskName != name)
                {
                    // 任务切换：上一条收尾
                    if (current.EndTime == default)
                        current.EndTime = entry.Timestamp.Value;
                    current = null;
                }

                if (current == null)
                {
                    // 从待执行队列取该任务的入口（按任务名匹配，未匹配则入口留空）
                    var entryName = "";
                    var keep = new Queue<(string Name, string Entry)>();
                    while (pending.Count > 0)
                    {
                        var p = pending.Dequeue();
                        if (p.Name == name && entryName == "")
                            entryName = p.Entry;
                        else
                            keep.Enqueue(p);
                    }
                    while (keep.Count > 0)
                        pending.Enqueue(keep.Dequeue());

                    current = new WorkRecord
                    {
                        TaskName = name,
                        Entry = entryName,
                        StartTime = entry.Timestamp.Value,
                        HasStarted = true,
                    };
                    records.Add(current);
                }
                // 同名任务新一轮：继续当前记录
                continue;
            }

            // 3. 队列结束：当前记录收尾并定状态（中断事件覆盖状态）
            var stop = StopStatusRegex.Match(entry.Content);
            if (stop.Success)
            {
                if (current != null && current.EndTime == default)
                    current.EndTime = entry.Timestamp.Value;
                if (current != null && StatusMap.TryGetValue(stop.Groups[1].Value, out var status))
                {
                    // NOT_STARTED 出现在「执行中停止」时（下一轮未开始）：有词条记录视为手动停止
                    var mapped = status;
                    if (status == "未开始" && current.HasRun)
                        mapped = "手动停止";
                    // 从未实际执行（无词条）的记录：NOT_STARTED/STOPPED 均视为「未开始」
                    if (!current.HasRun && mapped is "未开始" or "手动停止")
                        mapped = "未开始";
                    current.Status = current.HasInterrupt ? "中断" : mapped;
                }
                current = null;
                continue;
            }

            // 4. 词表行：归入当前记录（含短时间重复过滤）
            if (current != null)
                Accumulate(current, entry.Timestamp.Value, entry.Content, entry.Level, lastSeen);
        }

        // 日志未出现停止状态（进程被杀/断电）时，最后记录以最后一条事件时间收尾
        if (current != null && current.EndTime == default)
            current.EndTime = lastTime;

        // 从未开始、未产生业务数据的任务不显示，避免快速启动/回本丸等空记录污染列表
        records.RemoveAll(r => !r.HasStarted || r.Status == "未开始" || !r.HasRun);

        foreach (var record in records)
        {
            // 闪退场景无停止前状态行时中断状态兜底：有中断词条但未走到收尾逻辑的记录统一标记为中断
            if (record.HasInterrupt)
                record.Status = "中断";
        }
        return records;
    }

    // 刀种展示顺序（用户指定）：短刀→胁差→打刀→太刀→大太刀→枪→薙刀→剑
    public static readonly string[] SwordTypeOrder = ["短刀", "胁差", "打刀", "太刀", "大太刀", "枪", "薙刀", "剑"];

    // 短时间重复过滤窗口（秒）：识别循环连续命中同一 node 会重复输出同一词表行，窗口内只计一次
    private const double RepeatFilterSeconds = 3;

    // 提前结束类行为词
    private static readonly HashSet<string> EarlyEndActions = ["无票终止", "全部队伍不符合要求终止", "队长重伤撤退"];

    private static void Accumulate(WorkRecord record, DateTime time, string content, string level,
        Dictionary<WorkRecord, (DateTime Time, string Content)> lastSeen)
    {
        // 短时间重复过滤：同一内容在 3 秒窗口内重复出现只计一次
        if (lastSeen.TryGetValue(record, out var last)
            && last.Content == content
            && (time - last.Time).TotalSeconds <= RepeatFilterSeconds)
            return;
        lastSeen[record] = (time, content);

        var match = WordRegex.Match(content);
        if (!match.Success)
            return;
        var prefix = match.Groups[1].Value;
        var action = match.Groups[2].Value;
        var detail = match.Groups[3].Value;

        if (prefix == "中断")
        {
            record.HasInterrupt = true;
            record.SpecialEvents.Add(new SpecialEvent(time, action));
            return;
        }

        switch (action)
        {
            case "出阵":
                record.SortieCount++;
                break;
            case "点击行军":
                record.MarchCount++;
                break;
            case "完成一圈":
                record.RoundCount++;
                break;
            case "刷花":
                if (prefix == "后勤")
                    record.LogisticsCounts[action] = record.LogisticsCounts.GetValueOrDefault(action) + 1;
                else
                    record.FlowerBrushCount++;
                break;
            case "资源点获取":
                foreach (var part in detail.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    // 形如 木炭x20 / 玉钢x60
                    var idx = part.LastIndexOf('x');
                    if (idx <= 0 || !int.TryParse(part[(idx + 1)..], out var amount))
                        continue;
                    var name = part[..idx];
                    record.ResourceGains[name] = record.ResourceGains.GetValueOrDefault(name) + amount;
                }
                break;
            case "小判箱掉落":
                record.ResourceGains["小判箱"] = record.ResourceGains.GetValueOrDefault("小判箱") + 1;
                break;
            case "刀剑掉落":
                // 附加信息形如 太刀 狮子王（多空格）
                var parts = detail.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    record.SwordDrops.Add(new SwordDrop(parts[0], parts[1]));
                break;
            case "派遣远征":
                // 附加信息形如 部队3已派遣至 4-3
                record.LogisticsCounts["派遣远征"] = record.LogisticsCounts.GetValueOrDefault("派遣远征") + 1;
                if (TryParseDispatch(detail, out var unit, out var map))
                    record.LogisticsDispatches.Add(new LogisticsDispatch(time, unit, map));
                break;
            default:
                if (prefix == "后勤")
                {
                    record.LogisticsCounts[action] = record.LogisticsCounts.GetValueOrDefault(action) + 1;
                    if (EarlyEndActions.Contains(action))
                        record.EarlyEndCount++;
                }
                else if (EarlyEndActions.Contains(action))
                {
                    record.EarlyEndCount++;
                    if (level == "WRN")
                        record.SpecialEvents.Add(new SpecialEvent(time, action));
                }
                else if (level == "WRN")
                {
                    // 特殊情况只收 Warning 档词条（词表约定），Info 词条如命中王点/刷花不展示
                    record.SpecialEvents.Add(new SpecialEvent(time, action));
                }
                break;
        }
    }

    private static bool TryParseDispatch(string detail, out string unit, out string map)
    {
        unit = "";
        map = "";
        // 例：部队3已派遣至 4-3
        var m = Regex.Match(detail, @"^(部队\d+)已派遣至\s*(\S+)$");
        if (!m.Success)
            return false;
        unit = m.Groups[1].Value;
        map = m.Groups[2].Value;
        return true;
    }
}
