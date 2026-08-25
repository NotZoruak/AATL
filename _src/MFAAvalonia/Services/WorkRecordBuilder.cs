using System;
using System.Collections.Generic;
using System.Linq;
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
        var entryList = FillMissingInstanceIds(entries.ToList());
        var records = new List<WorkRecord>();
        // 按配置来源分组后各自流式解析。业务词条只要带有同一 cfg=，即可归入对应任务。
        // 无 cfg= 的行归入空组，保持旧日志的单配置行为。
        var configKeys = entryList
            .Select(GetGroupKey)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // 部分旧版日志没有配置上下文。只有一个配置来源时，可以安全地把无来源行并入该配置。
        if (configKeys.Count == 1)
        {
            var configKey = configKeys[0]!;
            var singleConfigEntries = entryList
                .Where(entry => string.IsNullOrWhiteSpace(GetGroupKey(entry))
                    || string.Equals(GetGroupKey(entry), configKey, StringComparison.Ordinal))
                .OrderBy(entry => entry.Timestamp)
                .ToList();
            records.AddRange(BuildGroup(singleConfigEntries, configKey));
            return records;
        }

        foreach (var group in entryList.GroupBy(GetGroupKey))
            records.AddRange(BuildGroup(group, group.Key));
        return records;
    }

    /// <summary>优先使用实例 ID；旧日志没有实例 ID 时回退到资源配置名。</summary>
    private static string GetGroupKey(LogEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.InstanceId)
            ? entry.InstanceId!
            : entry.ConfigSource ?? "";

    /// <summary>
    /// 为没有 inst 上下文的业务日志补全实例 ID。
    /// 部分自定义动作只输出 cfg，沿用日志中最近一次出现的 cfg-inst 映射。
    /// </summary>
    private static List<LogEntry> FillMissingInstanceIds(IReadOnlyList<LogEntry> entries)
    {
        var currentInstances = new Dictionary<string, string>(StringComparer.Ordinal);
        var result = new List<LogEntry>(entries.Count);
        foreach (var entry in entries.OrderBy(entry => entry.Timestamp))
        {
            if (string.IsNullOrWhiteSpace(entry.ConfigSource))
            {
                result.Add(entry);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.InstanceId))
            {
                currentInstances[entry.ConfigSource] = entry.InstanceId!;
                result.Add(entry);
                continue;
            }

            result.Add(currentInstances.TryGetValue(entry.ConfigSource, out var instanceId)
                ? entry with { InstanceId = instanceId }
                : entry);
        }

        return result;
    }

    /// <summary>单配置事件流的聚合主体。</summary>
    private static List<WorkRecord> BuildGroup(IEnumerable<LogEntry> entries, string configName)
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
                    // 新任务开始说明上一任务已正常完成；队列整体停止状态只会在最后统一输出。
                    current.Status = current.HasInterrupt ? "中断" : "成功";
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
                        ConfigName = configName,
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
                    current.IsClosedByStopStatus = true;
                }
                current = null;
                continue;
            }

            // 4. 词表行：归入当前记录（含短时间重复过滤）
            if (current != null)
            {
                var word = WordRegex.Match(entry.Content);
                var target = word.Success
                    ? FindRecordForPrefix(word.Groups[1].Value, records, current)
                    : current;
                if (target != null)
                    Accumulate(target, entry.Timestamp.Value, entry.Content, entry.Level, lastSeen);
            }
        }

        // 日志未出现停止状态（进程被杀/断电）时，最后记录以最后一条事件时间收尾
        if (current != null && current.EndTime == default)
            current.EndTime = lastTime;

        // 从未开始、未产生业务数据的任务不显示，避免快速启动/回本丸等空记录污染列表
        // 回本丸是流程任务，不是需要统计的业务任务；其中的返回本丸日志应归属于上一条出阵记录。
        records.RemoveAll(r => r.TaskName == "回本丸"
            || !r.HasStarted
            || r.Status == "未开始"
            || !r.HasRun);

        foreach (var record in records)
        {
            // 闪退场景无停止前状态行时中断状态兜底：有中断词条但未走到收尾逻辑的记录统一标记为中断
            if (record.HasInterrupt)
                record.Status = "中断";
        }
        return records;
    }

    /// <summary>按词条前缀寻找对应任务，支持地下城与后勤并行运行。</summary>
    private static WorkRecord? FindRecordForPrefix(string prefix, List<WorkRecord> records, WorkRecord current)
    {
        var taskName = prefix switch
        {
            "远征计时" => "后勤",
            _ => prefix,
        };

        if (taskName is "地下城" or "合战场" or "联队战" or "战术强化" or "后勤")
        {
            var matching = records.LastOrDefault(record =>
                record.HasStarted && record.TaskName == taskName && !record.IsClosedByStopStatus);
            if (matching != null)
                return matching;
        }

        return current;
    }

    // 刀种展示顺序（用户指定）：短刀→胁差→打刀→太刀→大太刀→枪→薙刀→剑
    public static readonly string[] SwordTypeOrder = ["短刀", "胁差", "打刀", "太刀", "大太刀", "枪", "薙刀", "剑"];

    // 短时间重复过滤窗口（秒）：识别循环连续命中同一 node 会重复输出同一词表行，窗口内只计一次
    private const double RepeatFilterSeconds = 3;

    private static void Accumulate(WorkRecord record, DateTime time, string content, string level,
        Dictionary<WorkRecord, (DateTime Time, string Content)> lastSeen)
    {
        var match = WordRegex.Match(content);

        // 小判箱弹窗会持续显示数秒，识别循环会连续产生多条相同日志。
        // 只要小判箱日志在事件流中连续出现，就视为同一次掉落，不受通用 3 秒窗口限制。
        if (match.Success
            && match.Groups[2].Value == "小判箱掉落"
            && lastSeen.TryGetValue(record, out var lastKoban)
            && lastKoban.Content == content)
            return;

        // 通用短时间重复过滤：同一内容在 3 秒窗口内重复出现只计一次
        if (lastSeen.TryGetValue(record, out var last)
            && last.Content == content
            && (time - last.Time).TotalSeconds <= RepeatFilterSeconds)
            return;
        lastSeen[record] = (time, content);

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
            case "返回本丸":
                record.ReturnHomeCount++;
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
            case "内番服":
                if (prefix == "后勤" && !string.IsNullOrWhiteSpace(detail))
                {
                    record.LogisticsCounts[action] = record.LogisticsCounts.GetValueOrDefault(action) + 1;
                    record.LogisticsNaibanOutfits.Add(new LogisticsNaibanOutfit(time, detail.Trim()));
                }
                break;
            case "开始修复":
                if (prefix == "后勤" && TryParseRepair(detail, out var repair))
                {
                    record.LogisticsCounts[action] = record.LogisticsCounts.GetValueOrDefault(action) + 1;
                    record.LogisticsRepairs.Add(repair with { Time = time });
                }
                break;
            case "补充刀装":
                if (prefix == "后勤")
                    record.LogisticsCounts[action] = record.LogisticsCounts.GetValueOrDefault(action) + 1;
                else
                    record.SpecialEvents.Add(new SpecialEvent(time, action));
                break;
            case "修复":
                if (level == "WRN")
                {
                    var repairDetail = string.IsNullOrWhiteSpace(detail) ? action : $"{action} {detail}";
                    record.SpecialEvents.Add(new SpecialEvent(time, repairDetail));
                }
                break;
            default:
                if (prefix == "远征计时" && action == "倒计时结束")
                {
                    record.LogisticsCounts[action] = record.LogisticsCounts.GetValueOrDefault(action) + 1;
                }
                else if (prefix == "后勤")
                {
                    record.LogisticsCounts[action] = record.LogisticsCounts.GetValueOrDefault(action) + 1;
                }
        else if (level == "WRN")
        {
            if (prefix == "RestartGameAction" && action == "ADB")
            {
                var warning = string.IsNullOrWhiteSpace(detail) ? "ADB 连接恢复流程" : detail;
                record.SpecialEvents.Add(new SpecialEvent(time, $"卡死重启：ADB 连接警告：{warning}"));
                return;
            }

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

    private static bool TryParseRepair(string detail, out LogisticsRepair repair)
    {
        repair = default!;
        var match = Regex.Match(detail, @"^(\S+)\s+(\d+)/(\d+)/(\d+)/(\d+)$");
        if (!match.Success)
            return false;

        repair = new LogisticsRepair(
            DateTime.MinValue,
            match.Groups[1].Value,
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value),
            int.Parse(match.Groups[4].Value),
            int.Parse(match.Groups[5].Value));
        return true;
    }
}
