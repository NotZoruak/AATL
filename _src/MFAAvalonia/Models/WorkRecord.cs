using System;
using System.Collections.Generic;

namespace MFAAvalonia.Models;

/// <summary>一条运行记录：队列中一个任务从开始到结束</summary>
public sealed class WorkRecord
{
    /// <summary>任务名（词表前缀，如「地下城」）</summary>
    public string TaskName { get; set; } = "";

    /// <summary>入口 pipeline（如 Underground）</summary>
    public string Entry { get; set; } = "";

    /// <summary>开始时间（任务定义行时间）</summary>
    public DateTime StartTime { get; set; }

    /// <summary>结束时间（下一任务定义行或队列停止行时间）</summary>
    public DateTime EndTime { get; set; }

    /// <summary>总耗时</summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>运行结果：成功/中断/手动停止/失败/未开始</summary>
    public string Status { get; set; } = "成功";

    /// <summary>记录期间是否出现中断事件（覆盖最终状态）</summary>
    public bool HasInterrupt { get; set; }

    /// <summary>任务是否实际开始执行过（存在「开始任务」行）</summary>
    public bool HasStarted { get; set; }

    /// <summary>任务是否产生过任何词条（有实际运行痕迹）</summary>
    public bool HasRun =>
        SortieCount > 0
        || MarchCount > 0
        || ResourceGains.Count > 0
        || SwordDrops.Count > 0
        || LogisticsCounts.Count > 0
        || SpecialEvents.Count > 0;

    /// <summary>列表行文本：08-18 12:00—13:27 地下城 (中断)</summary>
    public string ListLine =>
        $"{StartTime:MM-dd HH:mm}—{EndTime:HH:mm}  {TaskName}" +
        (Status == "成功" ? "" : $" ({Status})");

    /// <summary>记录列表中的时间文本</summary>
    public string ListTimeText => $"{StartTime:MM-dd HH:mm}–{EndTime:HH:mm} · {DurationText}";

    /// <summary>耗时文本：1 小时 27 分 / 35 分钟</summary>
    public string DurationText =>
        Duration.TotalMinutes < 60
            ? $"{Math.Max(1, (int)Math.Ceiling(Duration.TotalMinutes))} 分钟"
            : $"{(int)Duration.TotalHours} 小时 {Duration.Minutes} 分";

    /// <summary>出阵次数（「出阵」词条计数）</summary>
    public int SortieCount { get; set; }

    /// <summary>行军次数（「点击行军」词条计数）</summary>
    public int MarchCount { get; set; }

    /// <summary>完成圈数（「完成一圈」计数；无该词条的任务用出阵次数）</summary>
    public int RoundCount { get; set; }

    /// <summary>提前结束次数（无票终止/全部队伍不符合要求终止/队长重伤撤退）</summary>
    public int EarlyEndCount { get; set; }

    /// <summary>资源收获：资源名 → 累计数量（小判箱掉落按次数计入）</summary>
    public Dictionary<string, int> ResourceGains { get; } = [];

    /// <summary>刀剑掉落明细</summary>
    public List<SwordDrop> SwordDrops { get; } = [];

    /// <summary>后勤记录：行为词 → 次数（[后勤] 前缀词条）</summary>
    public Dictionary<string, int> LogisticsCounts { get; } = [];

    /// <summary>派遣远征明细（时间/部队/地图）</summary>
    public List<LogisticsDispatch> LogisticsDispatches { get; } = [];

    /// <summary>特殊情况：Warning 词条与中断事件（时间/描述）</summary>
    public List<SpecialEvent> SpecialEvents { get; } = [];
}

/// <summary>刀剑掉落明细条目</summary>
public sealed record SwordDrop(string SwordType, string SwordName);

/// <summary>派遣远征明细：部队 → 地图</summary>
public sealed record LogisticsDispatch(DateTime Time, string Unit, string Map);

/// <summary>特殊事件（Warning 词条或中断事件）</summary>
public sealed record SpecialEvent(DateTime Time, string Description);
