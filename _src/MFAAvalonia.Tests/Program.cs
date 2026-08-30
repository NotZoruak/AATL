using MFAAvalonia.Models;
using MFAAvalonia.Services;
using MFAAvalonia.Extensions.MaaFW.Custom;
using MFAAvalonia.Configuration;
using MFAAvalonia.Helper;
using MFAAvalonia.ViewModels.Pages;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

var actualWindowSize = WindowSizePersistence.GetValidSize(1366, 768);
AssertTrue(actualWindowSize is { Width: 1366, Height: 768 },
    "窗口保存应使用用户拖拽后的实际客户区尺寸");
var rootViewSource = File.ReadAllText(Path.Combine(
    Directory.GetCurrentDirectory(), "_src", "MFAAvalonia", "Views", "Windows", "RootView.axaml.cs"));
AssertTrue(rootViewSource.IndexOf("InitializeComponent();", StringComparison.Ordinal)
    < rootViewSource.IndexOf("LoadWindowSizeAndPosition();", StringComparison.Ordinal),
    "窗口应在加载已保存尺寸前完成XAML初始化，避免默认尺寸覆盖配置");

AssertTrue(MixGreedySelectionDecision.TryGetRarity(90, 90, 90, out var rarity) && rarity == 1,
    "稀有度1的颜色应正确映射");
AssertTrue(MixGreedySelectionDecision.TryGetRarity(101, 70, 23, out rarity) && rarity == 5,
    "稀有度5的颜色应正确映射");
AssertFalse(MixGreedySelectionDecision.TryGetRarity(100, 100, 100, out _),
    "未知颜色不应映射为稀有度");
AssertTrue(MixGreedySelectionDecision.CalculateRequiredMaterialCount(1, 1, 1) == 57,
    "稀有度1、乱舞1级且距下级1振时应还需57把素材");
AssertTrue(MixGreedySelectionDecision.CalculateRequiredMaterialCount(4, 5, 2) == 9,
    "稀有度4、乱舞5级且距下级2振时应还需9把素材");
AssertTrue(MixGreedySelectionDecision.TryParseSelectedCount(" 12／30 ", out var selected) && selected == 12,
    "应解析全角斜杠的已选数量");
AssertTrue(MixGreedySelectionDecision.TryParseSelectedCount("8/3O", out selected) && selected == 8,
    "应兼容容量30中的字母O误识别");
AssertFalse(MixGreedySelectionDecision.TryParseSelectedCount("无法识别", out _),
    "无斜杠的文本不应被识别为已选数量");
AssertTrue(MixGreedySelectionDecision.GetCancelCount(9, 30) == 21,
    "已选数量超额时应计算需取消的素材数");
AssertTrue(MixGreedySelectionDecision.GetCancelCount(30, 9) == -21,
    "已选数量不足时应保留负差值，以便直接习合");
var createPlanMethod = typeof(MixGreedySelectionDecision).GetMethod("CreatePlan");
var insufficientPlan = createPlanMethod?.Invoke(null, [10, 4]);
AssertTrue(insufficientPlan?.GetType().GetProperty("Mode")?.GetValue(insufficientPlan)?.ToString() == "Proceed",
    "素材不足时选材计划应直接进入习合");
var clearPlan = createPlanMethod?.Invoke(null, [6, 30]);
AssertTrue(clearPlan?.GetType().GetProperty("Mode")?.GetValue(clearPlan)?.ToString() == "ClearAndReselect",
    "超额超过15把时选材计划应全部解除后重选");
AssertFalse(MixGreedySelectionDecision.ShouldClearAllSelection(15),
    "超额15把时应逐把取消");
AssertTrue(MixGreedySelectionDecision.ShouldClearAllSelection(16),
    "超额超过15把时应先全部解除");
var manualClickAttemptsProperty = typeof(MixGreedySelectionDecision).GetProperty("ManualClickAttempts");
AssertTrue(manualClickAttemptsProperty?.GetValue(null) is 2,
    "手动选材首次未出现绿色状态时应重试一次");
var clearAllDelayProperty = typeof(MixGreedySelectionDecision).GetProperty("ClearAllDelayMilliseconds");
AssertTrue(clearAllDelayProperty?.GetValue(null) is 500,
    "全部解除后应等待500毫秒再检查首行绿色状态");
var clearAllAttemptsProperty = typeof(MixGreedySelectionDecision).GetProperty("ClearAllAttempts");
AssertTrue(clearAllAttemptsProperty?.GetValue(null) is 2,
    "首行仍为绿色时应最多再执行一次全部解除");
var swipeSettleDelayProperty = typeof(MixGreedySelectionDecision).GetProperty("SwipeSettleDelayMilliseconds");
AssertTrue(swipeSettleDelayProperty?.GetValue(null) is 500,
    "滑动结束后应等待500毫秒再检查素材选择状态");
var fallbackNeedRoi = typeof(MixGreedySelectionDecision).GetProperty("FallbackNeedOcrRoi")?.GetValue(null);
AssertTrue(fallbackNeedRoi?.ToString() == "MixGreedyRectangle { X = 431, Y = 342, Width = 15, Height = 17 }",
    "下一级需求的备用OCR区域应使用指定的小范围坐标");
var restoreAfterSwipeMethod = typeof(MixGreedySelectionDecision).GetMethod("ShouldRestoreLastSelectedMaterialAfterSwipe");
AssertTrue(restoreAfterSwipeMethod?.Invoke(null, [true, false]) is true,
    "滑动前第五行已选而滑动后第一行失去绿色状态时应恢复选择");
AssertTrue(restoreAfterSwipeMethod?.Invoke(null, [false, false]) is false,
    "滑动前第五行未选时不应恢复下一页第一行的选择");
AssertTrue(MixGreedySelectionDecision.CancelPositions.SequenceEqual([
    new MixGreedyPoint(857, 212),
    new MixGreedyPoint(858, 313),
    new MixGreedyPoint(858, 414),
    new MixGreedyPoint(858, 516),
    new MixGreedyPoint(858, 617),
]), "五行手动选材应仅将绿色状态识别点向左偏移242像素");

EdoRoutePlannerTests.Run();

AssertTrue(
    NewMixTargetSelectionDecision.Decide([
        new NewMixTargetSlot(false, false, null),
    ]).Outcome == NewMixTargetSelectionOutcome.NoSword,
    "一号位没有刀时应进入无刀分支");
AssertTrue(
    NewMixTargetSelectionDecision.Decide([
        new NewMixTargetSlot(true, false, 7),
        new NewMixTargetSlot(true, true, 7),
    ]) is { Outcome: NewMixTargetSelectionOutcome.Locked, Position: 2 },
    "带锁刀应无视乱舞等级优先进入专用链路");
AssertTrue(
    NewMixTargetSelectionDecision.Decide([
        new NewMixTargetSlot(true, false, 7),
        new NewMixTargetSlot(true, false, 6),
    ]) is { Outcome: NewMixTargetSelectionOutcome.Normal, Position: 2 },
    "未上锁且乱舞低于7级的刀应进入普通习合链路");
AssertTrue(
    NewMixTargetSelectionDecision.Decide([
        new NewMixTargetSlot(true, false, 7),
        new NewMixTargetSlot(true, false, 8),
    ]).Outcome == NewMixTargetSelectionOutcome.Completed,
    "所有可见未上锁刀均达到7级时应结束任务");
AssertTrue(
    NewMixTargetSelectionDecision.Decide([
        new NewMixTargetSlot(true, false, null),
        new NewMixTargetSlot(true, false, 6),
    ]) is { Outcome: NewMixTargetSelectionOutcome.Normal, Position: 2 },
    "未识别位置应跳过并继续选择后续可习合刀剑");

AssertTrue(EdoActionCountParser.Parse("６回") == 6,
    "行动次数 OCR 识别为全角数字时应仍能解析出次数");
AssertTrue(EdoActionCountParser.Parse("7回") == 7,
    "行动次数为七时应正确解析，不能误判为 OCR 失败");
AssertTrue(EdoActionCountParser.Parse("12回") == 12,
    "行动次数为两位数时应完整解析，不能只读取首位");
AssertTrue(EdoActionCountParser.Parse("１２回") == 12,
    "行动次数为全角两位数时应完整解析");
AssertTrue(EdoActionCountParser.Parse("冏一") == 1,
    "行动次数 OCR 识别为中文数字时应仍能解析出次数");
AssertTrue(EdoActionCountParser.Parse("冏12") == 12,
    "行动次数前缀被误识别时应仍能完整解析两位数字");
AssertTrue(EdoActionCountParser.Parse("一7") == 7,
    "同时出现汉字一与数字时应优先使用数字");
AssertTrue(EdoActionCountParser.Parse("二7") == 7,
    "同时出现其他汉字与数字时应忽略汉字");
AssertTrue(EdoActionCountParser.Parse("行动次数") == -1,
    "未识别到次数数字时应返回失败标记");
AssertTrue(EdoActionCountParser.Resolve(-1, "Start", 0) == -1,
    "开局行动次数无法 OCR 时不能假定固定次数");
AssertTrue(EdoActionCountParser.Resolve(-1, "P01", 6) == 6,
    "后续行动次数无法 OCR 时应使用已保存的剩余次数");

AssertTrue(RepairDetailFormatter.Format("太郎太刀", [632, 185, -1, 474]) == "太郎太刀 632/185/未识别/474",
    "修复资源部分 OCR 失败时应保留已识别的刀剑名和资源消耗");
AssertTrue(RepairDetailFormatter.Format("", [632, 185, -1, 474]) == "632/185/未识别/474",
    "修复刀剑名 OCR 失败时仍应输出已识别的资源消耗");

var emptyFilter = RepairFilterSelection.FromFlags(new Dictionary<string, bool>());
AssertFalse(emptyFilter.HasAnyFilter, "未选择任何筛选条件时不应启用筛选");

AssertTrue(SwordDropNotificationMatcher.ShouldNotify(true, ["今剑"], "今剑"),
    "开启播报且刀名在名单中时应触发通知");
AssertFalse(SwordDropNotificationMatcher.ShouldNotify(false, ["今剑"], "今剑"),
    "关闭播报开关时不应触发通知");
AssertFalse(SwordDropNotificationMatcher.ShouldNotify(true, ["厚藤四郎"], "今剑"),
    "识别到的刀名不在名单中时不应触发通知");
AssertTrue(SwordDropNotificationMatcher.FormatMessage("太刀", "狮子王") == "获得 太刀「狮子王」",
    "刀剑掉落通知文本应只包含获得信息");
AssertTrue(SwordDropNotificationMatcher.GetAnimationKind("特") == SwordDropAnimationKind.Specialization,
    "识别到特时应判定为特化动画");
AssertTrue(SwordDropNotificationMatcher.GetAnimationKind("极") == SwordDropAnimationKind.Kiwame,
    "识别到极时应判定为极化归来");
AssertTrue(SwordDropNotificationMatcher.GetAnimationKind("初") == SwordDropAnimationKind.InitialDrop,
    "识别到初时应判定为初始掉落");
AssertTrue(SwordDropNotificationMatcher.GetAnimationKind(" 初 ") == SwordDropAnimationKind.InitialDrop,
    "动画 OCR 结果应忽略空白字符");

var selectedFilter = RepairFilterSelection.FromFlags(new Dictionary<string, bool>
{
    ["sword_type_短"] = true,
    ["sword_type_太"] = true,
    ["damage_重伤"] = true,
});
AssertTrue(selectedFilter.HasAnyFilter, "选择刀种或伤势后应启用筛选");
AssertTrue(selectedFilter.SwordTypes.SetEquals(["短", "太"]), "刀种筛选应保留全部已选项");
AssertTrue(selectedFilter.DamageStates.SetEquals(["重伤"]), "伤势筛选应保留已选项");
AssertTrue(RepairFilterSelection.IsFilterTitle("师选"), "筛选标题 OCR 误识别为师选时仍应视为筛选标题");
AssertFalse(RepairFilterSelection.IsFilterTitle("刀剑男士"), "无关 OCR 文本不应视为筛选标题");

var cooldownStart = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
RepairCooldownState.Start(cooldownStart);
AssertTrue(RepairCooldownState.IsActive(cooldownStart.AddMinutes(29).AddSeconds(59)),
    "无可修刀后 30 分钟内应保持修刀冷却");
AssertFalse(RepairCooldownState.IsActive(cooldownStart.AddMinutes(30)),
    "修刀冷却达到 30 分钟后应自动恢复");

AssertTrue(FatigueCheckDecision.ShouldContinueWhenFirstValueUnreadable(null),
    "首位疲劳值无法识别时应继续出阵，不应进入刷花");
AssertFalse(FatigueCheckDecision.ShouldContinueWhenFirstValueUnreadable(29),
    "首位疲劳值低于阈值时应保留刷花分支");

AssertTrue(RepairListOcrDecision.IsSameValidResult("甲\n乙", "甲乙"),
    "滑动前后有效 OCR 文本相同应判定列表未变化");
AssertFalse(RepairListOcrDecision.IsSameValidResult("甲", "乙"),
    "滑动前后 OCR 文本变化时应继续扫描");
AssertFalse(RepairListOcrDecision.IsSameValidResult(null, null),
    "OCR 无结果时不应误判为列表到底");

var preset = new FormationPreset();

AssertFalse(preset.ClearEquipmentBeforeFormation, "新预设默认不应卸下现有装备");
AssertFalse(preset.SaveGameFormationRecordAfterFormation, "新预设默认不应保存游戏部队记录");

FormationPreset.SetRecordMode(preset, useRecordOnly: true);
AssertTrue(preset.UseGameFormationRecordOnly && !preset.SaveGameFormationRecordOnly,
    "仅使用编队记录与仅记录编队不能同时启用");
FormationPreset.SetRecordMode(preset, saveRecordOnly: true);
AssertTrue(!preset.UseGameFormationRecordOnly && preset.SaveGameFormationRecordOnly,
    "启用仅记录编队时应自动关闭仅使用编队记录");

var entry = new SwordBookEntry("3", "短刀", "今剑");
var editor = new SwordBookEditor(new[] { entry });
editor.SetOwned("3", SwordPortraitType.Wounded, true);
editor.Revert();
AssertFalse(editor.Entries[0].Wounded, "撤销应恢复上次保存的立绘状态");

editor.SetOwned("3", SwordPortraitType.Wounded, true);
editor.Save();
editor.SetOwned("3", SwordPortraitType.Wounded, false);
editor.Revert();
AssertTrue(editor.Entries[0].Wounded, "撤销应恢复已保存的立绘状态");

var logStart = new DateTime(2026, 8, 21, 3, 44, 55);
var dailyPipeline = JObject.Parse(File.ReadAllText(Path.Combine(
    Directory.GetCurrentDirectory(), "assets", "resource", "base", "pipeline", "DailyTask.json")));
for (var index = 1; index <= 5; index++)
{
    var enterTrainingAction = dailyPipeline[$"DT_DrillEnterTraining{index}"]?["action"];
    AssertTrue((string?)enterTrainingAction?["custom_action"] == "LogAction"
        && (string?)enterTrainingAction?["custom_action_param"]?["message"] == "[日课] 出阵",
        $"日课演练位置 {index} 进入战斗后应记录出阵");
}
var drillVictoryActionSource = File.ReadAllText(Path.Combine(
    Directory.GetCurrentDirectory(), "_src", "MFAAvalonia", "Extensions", "MaaFW", "Custom", "DrillVictoryAction.cs"));
AssertTrue(drillVictoryActionSource.Contains("LoggerHelper.Info(\"[日课] 完成一圈\");", StringComparison.Ordinal),
    "日课演练胜利后应记录完成一圈");

var missingInstanceRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "[cfg=Default][inst=配置 1/default] 开始任务：地下城", "Default", "default"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[cfg=Default] [地下城] 出阵", "Default"),
    new LogEntry(logStart.AddSeconds(2), "INF", "[cfg=Default][inst=配置 1/default] 停止前状态：SUCCEEDED", "Default", "default"),
]);
AssertTrue(missingInstanceRecord.Count == 1 && missingInstanceRecord[0].SortieCount == 1,
    "缺少实例标识的业务日志应继承相邻实例归属");
var parsedInstanceEntry = LogParser.ParseLines([
    "[2026-08-21 03:44:55.000][INF] [cfg=Default][inst=配置 1/default] [地下城] 出阵",
]).Single();
AssertTrue(parsedInstanceEntry.InstanceId == "default", "日志解析应提取实例 ID");
AssertTrue(parsedInstanceEntry.Content.Contains("[地下城] 出阵"), "日志解析应保留业务内容");

var instanceRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "[cfg=Default][inst=配置 1/default] 开始任务：地下城", "Default", "default"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[cfg=Default][inst=配置 1/default] [地下城] 出阵", "Default", "default"),
    new LogEntry(logStart.AddSeconds(2), "INF", "[cfg=Default][inst=配置 1/default] 停止前状态：SUCCEEDED", "Default", "default"),
]);
AssertTrue(instanceRecord.Count == 1 && instanceRecord[0].ConfigName == "default",
    "工作记录应使用实例 ID 作为配置归属");

var workRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(0), "INF", "[地下城] 小判箱掉落"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[地下城] 小判箱掉落"),
    new LogEntry(logStart.AddSeconds(5), "INF", "[地下城] 小判箱掉落"),
    new LogEntry(logStart.AddSeconds(6), "INF", "[地下城] 小判箱掉落"),
    new LogEntry(logStart.AddSeconds(7), "INF", "[地下城] 刀剑掉落 短刀 今剑"),
    new LogEntry(logStart.AddSeconds(8), "INF", "[地下城] 小判箱掉落"),
    new LogEntry(logStart.AddSeconds(12), "INF", "[地下城] 小判箱掉落"),
    new LogEntry(logStart.AddSeconds(13), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(workRecord.Count == 1, "测试日志应聚合为一条工作记录");
AssertTrue(workRecord[0].ResourceGains["小判箱"] == 2, "连续小判箱日志应各自只计为一次掉落");
AssertTrue(workRecord[0].Status == "成功", "存在成功停止状态时应显示成功");

var resourceGainRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[地下城] 资源点获取 木炭x20 玉钢x60"),
    new LogEntry(logStart.AddSeconds(2), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(resourceGainRecord.Count == 1
    && resourceGainRecord[0].ResourceGains.GetValueOrDefault("木炭") == 20
    && resourceGainRecord[0].ResourceGains.GetValueOrDefault("玉钢") == 60,
    "资源点获取打点应计入工作记录的资源获取");

var runningRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[地下城] 点击行军"),
]);
AssertTrue(runningRecord.Count == 1, "运行中的任务应保留为一条工作记录");
AssertTrue(runningRecord[0].Status == "进行中", "没有停止状态的运行记录应显示进行中");

var swordBookScanRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[地下城] 点击行军"),
    new LogEntry(logStart.AddSeconds(2), "INF", "开始任务：刀帐自动识别"),
    new LogEntry(logStart.AddSeconds(3), "WRN", "[刀帐] 自动识别失败"),
    new LogEntry(logStart.AddSeconds(4), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(swordBookScanRecords.Count == 1 && swordBookScanRecords[0].TaskName == "地下城",
    "刀帐自动识别不应单独显示，也不应作为上一任务的业务记录");
AssertTrue(swordBookScanRecords[0].SpecialEvents.Count == 0,
    "刀帐自动识别期间的日志不应附加到上一条业务记录");

var mixRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：习合"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[习合] 完成一圈"),
    new LogEntry(logStart.AddSeconds(2), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(mixRecords.Count == 0,
    "习合搓糖任务不应显示在工作记录中");

var labeledMixRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：习合搓糖"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[习合] 完成一圈"),
    new LogEntry(logStart.AddSeconds(2), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(labeledMixRecords.Count == 0,
    "以习合搓糖标签启动的任务不应显示在工作记录中");

var adbWarningRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "WRN", "[RestartGameAction] ADB 命令返回警告: device offline"),
]);
AssertTrue(
    adbWarningRecord[0].SpecialEvents.Count == 1
        && adbWarningRecord[0].SpecialEvents[0].Description.Contains("卡死重启")
        && adbWarningRecord[0].SpecialEvents[0].Description.Contains("device offline"),
    "卡死重启期间的 ADB 警告应以可读文本显示在特殊情况中");

var earlyEndRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：合战场"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[合战场] 出阵"),
    new LogEntry(logStart.AddSeconds(2), "INF", "[合战场] 道中撤退"),
    new LogEntry(logStart.AddSeconds(3), "INF", "停止前状态：STOPPED"),
]);
AssertTrue(earlyEndRecord.Count == 1 && earlyEndRecord[0].ReturnHomeCount == 0,
    "撤退原因本身不应重复计入返回本丸次数");

var returnHomeRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[地下城] 返回本丸"),
    new LogEntry(logStart.AddSeconds(2), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(returnHomeRecord.Count == 1 && returnHomeRecord[0].ReturnHomeCount == 1,
    "确认返回本丸日志应计入返回本丸次数");

var undergroundEarlyEndRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "WRN", "[地下城] 刀装近破坏撤退"),
    new LogEntry(logStart.AddSeconds(2), "INF", "[远征计时] 倒计时结束"),
    new LogEntry(logStart.AddSeconds(3), "INF", "[地下城] 返回本丸"),
    new LogEntry(logStart.AddSeconds(4), "INF", "停止前状态：STOPPED"),
]);
AssertTrue(undergroundEarlyEndRecord.Count == 1 && undergroundEarlyEndRecord[0].ReturnHomeCount == 1,
    "地下城应只按确认返回本丸日志计数");
AssertTrue(undergroundEarlyEndRecord[0].SpecialEvents.Exists(e => e.Description == "刀装近破坏撤退"),
    "刀装近破坏撤退仍应作为特殊情况记录");
AssertTrue(undergroundEarlyEndRecord[0].LogisticsCounts["倒计时结束"] == 1,
    "远征倒计时结束应计入后勤记录");

var dragCaptainWarningRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：江户潜入"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[江户潜入] 出阵"),
    new LogEntry(logStart.AddSeconds(2), "WRN", "[DragCaptain] 无可用位置（空槽位或 OCR 失败），跳过拖拽"),
    new LogEntry(logStart.AddSeconds(3), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(dragCaptainWarningRecord[0].SpecialEvents.Count == 0,
    "换队长拖拽的无可用位置保护日志不应显示为特殊情况");

var switchedTaskRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：异去"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[异去] 完成一圈"),
    new LogEntry(logStart.AddSeconds(2), "INF", "开始任务：回本丸"),
    new LogEntry(logStart.AddSeconds(3), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(switchedTaskRecords.Count == 1 && switchedTaskRecords[0].TaskName == "异去",
    "任务切换时应保留上一条有业务数据的记录");
AssertTrue(switchedTaskRecords[0].Status == "成功", "正常切换到下一个任务时上一条记录应显示成功");

var returnHomeTaskRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：回本丸"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[地下城] 返回本丸"),
    new LogEntry(logStart.AddSeconds(2), "INF", "停止前状态：SUCCEEDED"),
]);
AssertTrue(returnHomeTaskRecords.All(record => record.TaskName != "回本丸"),
    "回本丸流程不应作为独立工作记录显示");

var parallelTaskRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "[任务管线合并] 任务#1 名称=[地下城] 入口=[Underground]"),
    new LogEntry(logStart, "INF", "[任务管线合并] 任务#2 名称=[后勤] 入口=[Expedition]"),
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "INF", "开始任务：后勤"),
    new LogEntry(logStart.AddSeconds(2), "INF", "[entry=Underground] [地下城] 刀剑掉落 短刀 秋田藤四郎"),
    new LogEntry(logStart.AddSeconds(3), "INF", "[entry=Expedition] [后勤] 派遣远征 部队1已派遣至 2-4"),
    new LogEntry(logStart.AddSeconds(4), "INF", "停止前状态：SUCCEEDED"),
]);
var parallelDungeon = parallelTaskRecords.Single(record => record.TaskName == "地下城");
var parallelLogistics = parallelTaskRecords.Single(record => record.TaskName == "后勤");
AssertTrue(parallelDungeon.SwordDrops.Count == 1, "并行任务中地下城掉落应归入地下城记录");
AssertTrue(parallelLogistics.SwordDrops.Count == 0, "并行任务中后勤记录不应包含地下城掉落");

var renamedParallelTaskRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "[任务管线合并] 任务#1 名称=[大阪挖地] 入口=[Underground]"),
    new LogEntry(logStart, "INF", "[任务管线合并] 任务#2 名称=[本丸后勤] 入口=[Expedition]"),
    new LogEntry(logStart.AddSeconds(1), "INF", "开始任务：大阪挖地"),
    new LogEntry(logStart.AddSeconds(2), "INF", "开始任务：本丸后勤"),
    new LogEntry(logStart.AddSeconds(3), "INF", "[地下城] 刀剑掉落 短刀 秋田藤四郎"),
    new LogEntry(logStart.AddSeconds(4), "INF", "[后勤] 派遣远征 部队1已派遣至 2-4"),
    new LogEntry(logStart.AddSeconds(5), "INF", "停止前状态：SUCCEEDED"),
]);
var renamedDungeon = renamedParallelTaskRecords.Single(record => record.TaskName == "大阪挖地");
var renamedLogistics = renamedParallelTaskRecords.Single(record => record.TaskName == "本丸后勤");
AssertTrue(renamedDungeon.SwordDrops.Count == 1, "大阪挖地的地下城掉落应按 Entry 归属");
AssertTrue(renamedLogistics.LogisticsCounts["派遣远征"] == 1, "改名后的后勤日志应归入本丸后勤记录");

var remarkedParallelTaskRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "[任务管线合并] 任务#1 名称=[我的地下城备注] 入口=[Underground]"),
    new LogEntry(logStart, "INF", "[任务管线合并] 任务#2 名称=[我的后勤备注] 入口=[Expedition]"),
    new LogEntry(logStart.AddSeconds(1), "INF", "开始任务：我的地下城备注"),
    new LogEntry(logStart.AddSeconds(2), "INF", "开始任务：我的后勤备注"),
    new LogEntry(logStart.AddSeconds(3), "INF", "[地下城] 刀剑掉落 短刀 秋田藤四郎"),
    new LogEntry(logStart.AddSeconds(4), "INF", "[后勤] 派遣远征 部队1已派遣至 2-4"),
    new LogEntry(logStart.AddSeconds(5), "INF", "停止前状态：SUCCEEDED"),
]);
var remarkedDungeon = remarkedParallelTaskRecords.FirstOrDefault(record => record.TaskName == "我的地下城备注");
var remarkedLogistics = remarkedParallelTaskRecords.FirstOrDefault(record => record.TaskName == "我的后勤备注");
AssertTrue(remarkedDungeon != null, "自定义备注的地下城任务应保留为工作记录");
AssertTrue(remarkedLogistics != null, "自定义备注的后勤任务应保留为工作记录");
if (remarkedDungeon != null && remarkedLogistics != null)
{
    AssertTrue(remarkedDungeon.SwordDrops.Count == 1, "自定义备注不应影响地下城业务日志归属");
    AssertTrue(remarkedLogistics.LogisticsCounts["派遣远征"] == 1, "自定义备注不应影响后勤业务日志归属");
}

var finishedLogisticsThenDungeonRecords = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：后勤"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[后勤] 检查队伍状况"),
    new LogEntry(logStart.AddSeconds(2), "INF", "停止前状态：SUCCEEDED"),
    new LogEntry(logStart.AddSeconds(3), "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(4), "INF", "[后勤] 派遣远征 部队1已派遣至 2-4"),
    new LogEntry(logStart.AddSeconds(5), "INF", "[地下城] 点击行军"),
]);
var laterDungeon = finishedLogisticsThenDungeonRecords.Single(record => record.TaskName == "地下城");
AssertTrue(laterDungeon.LogisticsCounts["派遣远征"] == 1,
    "已结束的后勤记录不应接收后续地下城运行期间的后勤词条");

var supplementRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[地下城] 补充刀装"),
    new LogEntry(logStart.AddSeconds(2), "INF", "停止前状态：SUCCEEDED"),
]).Single();
AssertTrue(supplementRecord.SpecialEvents.Any(item => item.Description == "补充刀装"),
    "出阵任务产生的补充刀装应显示在特殊情况中");

var logisticsSupplementRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：后勤"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[后勤] 补充刀装"),
    new LogEntry(logStart.AddSeconds(2), "INF", "停止前状态：SUCCEEDED"),
]).Single();
AssertTrue(logisticsSupplementRecord.LogisticsCounts["补充刀装"] == 1,
    "后勤任务产生的补充刀装应显示在后勤记录中");

var firstSavedSource = new WorkRecord
{
    TaskName = "地下城",
    StartTime = new DateTime(2026, 8, 20, 10, 0, 0),
    EndTime = new DateTime(2026, 8, 20, 10, 20, 0),
    Status = "成功",
    SortieCount = 1,
    RoundCount = 2,
};
firstSavedSource.ResourceGains["木炭"] = 120;
var secondSavedSource = new WorkRecord
{
    TaskName = "地下城",
    StartTime = new DateTime(2026, 8, 21, 11, 0, 0),
    EndTime = new DateTime(2026, 8, 21, 11, 30, 0),
    Status = "成功",
    SortieCount = 2,
    RoundCount = 3,
};
secondSavedSource.ResourceGains["木炭"] = 80;
var merged = SavedWorkRecordService.Merge([firstSavedSource, secondSavedSource], "地下城周回");
AssertTrue(merged.DisplayName == "地下城周回", "合并记录应保留用户输入的记录名");
AssertTrue(merged.TaskName == "地下城", "合并记录应保留原任务名");
AssertTrue(merged.StartDate == firstSavedSource.StartTime.Date, "合并记录应取最早日期");
AssertTrue(merged.EndDate == secondSavedSource.EndTime.Date, "合并记录应取最晚日期");
AssertTrue(merged.Duration == TimeSpan.FromMinutes(50), "合并记录应累加持续时间");
AssertTrue(merged.SortieCount == 3 && merged.RoundCount == 5, "合并记录应累加出阵统计");
AssertTrue(merged.ResourceGains["木炭"] == 200, "合并记录应累加资源收获");

var renamedMergeSource = new WorkRecord
{
    TaskName = "大阪挖地",
    Entry = "Underground",
    StartTime = new DateTime(2026, 8, 22, 10, 0, 0),
    EndTime = new DateTime(2026, 8, 22, 10, 20, 0),
    Status = "成功",
    SortieCount = 1,
};
var legacyMergeSource = new WorkRecord
{
    TaskName = "地下城",
    Entry = "Underground",
    StartTime = new DateTime(2026, 8, 21, 10, 0, 0),
    EndTime = new DateTime(2026, 8, 21, 10, 20, 0),
    Status = "成功",
    SortieCount = 2,
};
var renamedMerged = SavedWorkRecordService.Merge([legacyMergeSource, renamedMergeSource], "大阪挖地合并");
AssertTrue(renamedMerged.SortieCount == 3, "新旧任务名称应允许合并实时工作记录");
var renamedSavedMerged = SavedWorkRecordService.Merge(
    [SavedWorkRecordService.Save(legacyMergeSource, "旧地下城"),
     SavedWorkRecordService.Save(renamedMergeSource, "新大阪挖地")],
    "大阪挖地保存记录合并");
AssertTrue(renamedSavedMerged.Segments.Count == 2, "新旧任务名称应允许合并已保存工作记录");

var customRemarkOldSource = new WorkRecord
{
    TaskName = "地下城旧备注",
    Entry = "Underground",
    StartTime = new DateTime(2026, 8, 23, 10, 0, 0),
    EndTime = new DateTime(2026, 8, 23, 10, 20, 0),
    Status = "成功",
};
var customRemarkNewSource = new WorkRecord
{
    TaskName = "大阪挖地新备注",
    Entry = "Underground",
    StartTime = new DateTime(2026, 8, 24, 10, 0, 0),
    EndTime = new DateTime(2026, 8, 24, 10, 20, 0),
    Status = "成功",
};
var customRemarkMerged = SavedWorkRecordService.Merge(
    [customRemarkOldSource, customRemarkNewSource], "备注任务合并");
AssertTrue(customRemarkMerged.Duration == TimeSpan.FromMinutes(40),
    "相同 pipeline 入口的不同任务备注应允许合并");
var customRemarkSavedMerged = SavedWorkRecordService.Merge(
    [SavedWorkRecordService.Save(customRemarkOldSource, "旧备注记录"),
     SavedWorkRecordService.Save(customRemarkNewSource, "新备注记录")],
    "备注保存记录合并");
AssertTrue(customRemarkSavedMerged.Entry == "Underground" && customRemarkSavedMerged.Segments.Count == 2,
    "相同 pipeline 入口的不同任务备注应允许合并已保存记录并保留入口");

var duplicateMerged = SavedWorkRecordService.Merge(
    [firstSavedSource, firstSavedSource],
    "地下城重复合并");
AssertTrue(duplicateMerged.Duration == TimeSpan.FromMinutes(20), "相同时间段合并时不应重复累加时长");
AssertTrue(duplicateMerged.SortieCount == 1, "相同时间段合并时不应重复累加出阵次数");
AssertTrue(duplicateMerged.ResourceGains["木炭"] == 120, "相同时间段合并时不应重复累加资源收获");
var savedDuplicateMerged = SavedWorkRecordService.Merge(
    [SavedWorkRecordService.Save(firstSavedSource, "地下城"),
     SavedWorkRecordService.Save(firstSavedSource, "地下城")],
    "地下城保存记录重复合并");
AssertTrue(savedDuplicateMerged.Duration == TimeSpan.FromMinutes(20), "已保存记录按相同时间段合并时不应重复累加时长");
AssertTrue(savedDuplicateMerged.ResourceGains["木炭"] == 120, "已保存记录按相同时间段合并时不应重复累加资源收获");

var name = SavedWorkRecordService.CreateUniqueName("地下城", ["地下城", "地下城（1）"]);
AssertTrue(name == "地下城（2）", "重名保存记录应自动追加递增编号");

var savedPath = Path.Combine(Path.GetTempPath(), $"matr-saved-{Guid.NewGuid():N}.json");
SavedWorkRecordStore.Save(savedPath, [merged]);
var loaded = SavedWorkRecordStore.Load(savedPath);
File.Delete(savedPath);
AssertTrue(loaded.Count == 1 && loaded[0].DisplayName == "地下城周回", "保存记录应能从本地文件恢复");
AssertTrue(loaded[0].Segments.Count == 2, "保存记录应保留每一段记录的精确时间");

var warehouseEditor = new WarehouseDataEditor(new WarehouseData
{
    CoreResources = new Dictionary<string, int> { ["木炭"] = 100 },
});
warehouseEditor.Data.CoreResources["木炭"] = 120;
AssertTrue(warehouseEditor.HasChanges, "修改仓库资源后应标记为未保存");
warehouseEditor.Revert();
AssertTrue(warehouseEditor.Data.CoreResources["木炭"] == 100, "撤销应恢复已保存的仓库资源");
warehouseEditor.Data.CoreResources["木炭"] = 150;
warehouseEditor.Save();
AssertFalse(warehouseEditor.HasChanges, "保存仓库数据后不应继续标记为未保存");
warehouseEditor.Clear();
AssertTrue(warehouseEditor.Data.CoreResources.Count == 0, "清空应移除仓库资源数据");

AssertTrue(WarehouseScanDraftService.TryParseCount("1,234", out var commaValue) && commaValue == 1234,
    "OCR 数值应清除英文逗号");
AssertTrue(WarehouseScanDraftService.TryParseCount("1，234", out var chineseCommaValue) && chineseCommaValue == 1234,
    "OCR 数值应清除中文逗号");
AssertTrue(WarehouseScanDraftService.TryParseCount("1.234", out var dotValue) && dotValue == 1234,
    "OCR 数值应清除分隔点");
AssertTrue(WarehouseScanDraftService.TryParseCount("所持小判 3,750,570 枚", out var kobanValue) && kobanValue == 3750570,
    "OCR 数值应能从资源名称和单位中提取数字");
AssertFalse(WarehouseScanDraftService.TryParseCount("无法识别", out _),
    "非法 OCR 文本不应被解析为资源数量");

var draftPath = Path.Combine(Path.GetTempPath(), $"matr-warehouse-{Guid.NewGuid():N}.json");
WarehouseScanDraftService.UpdateCoreResource(draftPath, "木炭", 1234);
WarehouseScanDraftService.UpdateCoreResource(draftPath, "玉钢", 5678);
var warehouseDraft = WarehouseScanDraftService.Load(draftPath);
File.Delete(draftPath);
AssertTrue(warehouseDraft.CoreResources["木炭"] == 1234 && warehouseDraft.CoreResources["玉钢"] == 5678,
    "仓库识别草稿应保留已识别的核心资源");

var historyPath = Path.Combine(Path.GetTempPath(), $"matr-warehouse-history-{Guid.NewGuid():N}.json");
WarehouseScanDraftService.AppendSnapshot(historyPath, new Dictionary<string, int>
{
    ["木炭"] = 1234,
    ["玉钢"] = 5678,
});
var historyDraft = WarehouseScanDraftService.Load(historyPath);
File.Delete(historyPath);
AssertTrue(historyDraft.ResourceHistory.Count == 1
    && historyDraft.ResourceHistory[0].Values["木炭"] == 1234,
    "完整仓库识别结束后应追加核心资源历史快照");

var updateDataConfigRoot = Path.Combine(Path.GetTempPath(), $"matr-update-data-config-{Guid.NewGuid():N}");
Directory.CreateDirectory(updateDataConfigRoot);
AppPaths.InstancesDirectory = updateDataConfigRoot;
try
{
    var sameDay = new DateTime(2026, 8, 26, 9, 30, 0, DateTimeKind.Local);
    var updateDataConfig = new InstanceConfiguration("update-data-first-run");
    AssertTrue(InvokeUpdateDataShouldRun(updateDataConfig, "每天", sameDay),
        "更新数据在没有成功时间时应允许运行");
    AssertTrue(InvokeUpdateDataGetLastSucceeded(updateDataConfig) == null,
        "缺少成功时间时应返回空值");

    InvokeUpdateDataMarkSucceeded(updateDataConfig, sameDay);
    AssertTrue(InvokeUpdateDataShouldRun(updateDataConfig, "每次", sameDay),
        "触发间隔为每次时不应受上次成功时间影响");
    AssertTrue(InvokeUpdateDataGetLastSucceeded(updateDataConfig) == sameDay,
        "记录成功时间后应按原值读回");

    var reloadedConfig = new InstanceConfiguration("update-data-first-run");
    AssertTrue(File.Exists(reloadedConfig.GetConfigFilePath()),
        "记录成功时间后应生成实例配置文件");
    var reloadedSucceededAt = InvokeUpdateDataGetLastSucceeded(reloadedConfig);
    AssertTrue(reloadedSucceededAt == sameDay,
        "重新加载实例配置后应能从真实配置文件读回成功时间");

    var isolatedConfig = new InstanceConfiguration("update-data-second-instance");
    AssertTrue(InvokeUpdateDataGetLastSucceeded(isolatedConfig) == null,
        "不同实例的成功时间记录不应互相影响");
    AssertTrue(InvokeUpdateDataShouldRun(isolatedConfig, "每天", sameDay),
        "其他实例没有成功时间时仍应允许运行");

    var dailyConfig = new InstanceConfiguration("update-data-daily");
    InvokeUpdateDataMarkSucceeded(dailyConfig, sameDay);
    AssertFalse(InvokeUpdateDataShouldRun(dailyConfig, "每天", sameDay.AddHours(2)),
        "每天触发间隔在同一本地日期内应跳过");
    AssertTrue(InvokeUpdateDataShouldRun(dailyConfig, "每天", sameDay.AddDays(1)),
        "每天触发间隔跨天后应重新运行");

    var weeklyConfig = new InstanceConfiguration("update-data-weekly");
    InvokeUpdateDataMarkSucceeded(weeklyConfig, new DateTime(2026, 8, 26, 9, 30, 0, DateTimeKind.Local));
    AssertFalse(InvokeUpdateDataShouldRun(weeklyConfig, "每周", new DateTime(2026, 8, 28, 9, 30, 0, DateTimeKind.Local)),
        "每周触发间隔在同一 ISO 周内应跳过");
    AssertTrue(InvokeUpdateDataShouldRun(weeklyConfig, "每周", new DateTime(2026, 8, 31, 9, 30, 0, DateTimeKind.Local)),
        "每周触发间隔跨 ISO 周后应重新运行");

    var invalidTimeConfig = new InstanceConfiguration("update-data-invalid");
    invalidTimeConfig.SetValue(ConfigurationKeys.UpdateDataLastSucceededAt, "not-a-time");
    AssertTrue(InvokeUpdateDataShouldRun(invalidTimeConfig, "每天", sameDay),
        "损坏的成功时间记录不应阻止更新数据运行");
    AssertTrue(InvokeUpdateDataGetLastSucceeded(invalidTimeConfig) == null,
        "损坏的成功时间记录应按空值处理");
}
finally
{
    DeleteDirectoryIfExists(updateDataConfigRoot);
}

ConfigurationManager.Current.Reset();
var oldWarehouse = new WarehouseData
{
    CoreResources = new Dictionary<string, int> { ["木炭"] = 10, ["玉钢"] = 20 },
    OtherItems = new Dictionary<string, int> { ["御守·桃"] = 1 },
};
var oldSwordBook = new List<SwordBookPortraitState>
{
    new("1", true, false, false, false, false),
};
ConfigurationManager.Current.SetValue(ConfigurationKeys.WarehouseData, oldWarehouse.Clone());
ConfigurationManager.Current.SetValue(ConfigurationKeys.SwordBookEntries, CloneSwordBookStates(oldSwordBook));

var warehouseDraftSavePath = Path.Combine(Path.GetTempPath(), $"matr-update-data-warehouse-{Guid.NewGuid():N}.json");
File.WriteAllText(warehouseDraftSavePath,
    """
    {
      "core_resources": {
        "木炭": 123,
        "小判": 456
      },
      "other_items": {
        "御守桃": 2
      },
      "resource_history": [
        {
          "recorded_at": "2026-08-25T12:00:00+08:00",
          "values": {
            "木炭": 120
          }
        }
      ]
    }
    """);
AssertTrue(InvokeTrySaveWarehouseDraft(warehouseDraftSavePath),
    "有效的仓库识别草稿应能写入正式仓库数据");
DeleteIfExists(warehouseDraftSavePath);

var savedWarehouse = ConfigurationManager.Current.GetValue(ConfigurationKeys.WarehouseData, new WarehouseData());
var untouchedSwordBook = ConfigurationManager.Current.GetValue(ConfigurationKeys.SwordBookEntries, new List<SwordBookPortraitState>());
AssertTrue(savedWarehouse.CoreResources["木炭"] == 123 && savedWarehouse.CoreResources["小判"] == 456,
    "保存仓库草稿后应使用草稿中的核心资源覆盖正式数据");
AssertTrue(savedWarehouse.OtherItems.ContainsKey("御守·桃") && savedWarehouse.OtherItems["御守·桃"] == 2,
    "保存仓库草稿时应复用现有名称归一化逻辑");
AssertTrue(savedWarehouse.ResourceHistory.Count == 1 && savedWarehouse.ResourceHistory[0].Values["木炭"] == 120,
    "保存仓库草稿后应保留草稿中的历史快照");
AssertTrue(AreSameSwordBookStates(untouchedSwordBook, oldSwordBook),
    "保存仓库草稿时不应改写刀帐正式数据");

ConfigurationManager.Current.Reset();
ConfigurationManager.Current.SetValue(ConfigurationKeys.WarehouseData, oldWarehouse.Clone());
ConfigurationManager.Current.SetValue(ConfigurationKeys.SwordBookEntries, CloneSwordBookStates(oldSwordBook));
var invalidWarehouseDraftPath = Path.Combine(Path.GetTempPath(), $"matr-update-data-warehouse-invalid-{Guid.NewGuid():N}.json");
File.WriteAllText(invalidWarehouseDraftPath, "{ invalid json");
AssertFalse(InvokeTrySaveWarehouseDraft(invalidWarehouseDraftPath),
    "损坏的仓库识别草稿不应写入正式数据");
DeleteIfExists(invalidWarehouseDraftPath);
var unchangedWarehouse = ConfigurationManager.Current.GetValue(ConfigurationKeys.WarehouseData, new WarehouseData());
var unchangedSwordBook = ConfigurationManager.Current.GetValue(ConfigurationKeys.SwordBookEntries, new List<SwordBookPortraitState>());
AssertTrue(unchangedWarehouse.CoreResources["木炭"] == 10 && unchangedWarehouse.CoreResources["玉钢"] == 20,
    "仓库草稿损坏时应保留原有仓库数据");
AssertTrue(AreSameSwordBookStates(unchangedSwordBook, oldSwordBook),
    "仓库草稿损坏时不应影响刀帐数据");

ConfigurationManager.Current.Reset();
ConfigurationManager.Current.SetValue(ConfigurationKeys.WarehouseData, oldWarehouse.Clone());
ConfigurationManager.Current.SetValue(ConfigurationKeys.SwordBookEntries, CloneSwordBookStates(oldSwordBook));
var swordBookDraftPath = Path.Combine(Path.GetTempPath(), $"matr-update-data-swordbook-{Guid.NewGuid():N}.json");
File.WriteAllText(swordBookDraftPath,
    """
    [
      {
        "Number": "3",
        "Owned": true,
        "Wounded": true,
        "TrueSword": false,
        "InnerCare": false,
        "Casual": true
      }
    ]
    """);
AssertTrue(InvokeTrySaveSwordBookDraft(swordBookDraftPath),
    "有效的刀帐识别草稿应能写入正式刀帐数据");
DeleteIfExists(swordBookDraftPath);
var savedSwordBook = ConfigurationManager.Current.GetValue(ConfigurationKeys.SwordBookEntries, new List<SwordBookPortraitState>());
var untouchedWarehouse = ConfigurationManager.Current.GetValue(ConfigurationKeys.WarehouseData, new WarehouseData());
AssertTrue(savedSwordBook.Count == 1
    && savedSwordBook[0].Number == "3"
    && savedSwordBook[0].Owned
    && savedSwordBook[0].Wounded
    && savedSwordBook[0].Casual,
    "保存刀帐草稿后应使用草稿中的拥有状态覆盖正式数据");
AssertTrue(untouchedWarehouse.CoreResources["木炭"] == 10 && untouchedWarehouse.CoreResources["玉钢"] == 20,
    "保存刀帐草稿时不应改写仓库正式数据");

ConfigurationManager.Current.Reset();
ConfigurationManager.Current.SetValue(ConfigurationKeys.WarehouseData, oldWarehouse.Clone());
ConfigurationManager.Current.SetValue(ConfigurationKeys.SwordBookEntries, CloneSwordBookStates(oldSwordBook));
var invalidSwordBookDraftPath = Path.Combine(Path.GetTempPath(), $"matr-update-data-swordbook-invalid-{Guid.NewGuid():N}.json");
File.WriteAllText(invalidSwordBookDraftPath,
    """
    [
      {
        "Number": "",
        "Owned": true,
        "Wounded": false,
        "TrueSword": false,
        "InnerCare": false,
        "Casual": false
      }
    ]
    """);
AssertFalse(InvokeTrySaveSwordBookDraft(invalidSwordBookDraftPath),
    "损坏的刀帐识别草稿不应写入正式数据");
DeleteIfExists(invalidSwordBookDraftPath);
var unchangedSwordBookAfterInvalid = ConfigurationManager.Current.GetValue(ConfigurationKeys.SwordBookEntries, new List<SwordBookPortraitState>());
var unchangedWarehouseAfterSwordBookInvalid = ConfigurationManager.Current.GetValue(ConfigurationKeys.WarehouseData, new WarehouseData());
AssertTrue(AreSameSwordBookStates(unchangedSwordBookAfterInvalid, oldSwordBook),
    "刀帐草稿损坏时应保留原有刀帐数据");
AssertTrue(unchangedWarehouseAfterSwordBookInvalid.CoreResources["木炭"] == 10
    && unchangedWarehouseAfterSwordBookInvalid.CoreResources["玉钢"] == 20,
    "刀帐草稿损坏时不应影响仓库数据");

var naibanOutfitState = new NaibanOutfitRecognitionState();
naibanOutfitState.Begin();
AssertTrue(naibanOutfitState.TryRecord("今剑"), "首次识别到的内番服应被记录");
AssertFalse(naibanOutfitState.TryRecord("今剑"), "同一把刀剑的动画重复命中不应重复记录");
AssertTrue(naibanOutfitState.TryRecord("秋田藤四郎"), "同一次内番安排应允许记录第二把刀剑");
AssertFalse(naibanOutfitState.TryRecord("厚藤四郎"), "同一次内番安排最多记录两把刀剑");
AssertFalse(naibanOutfitState.ShouldLogMissingOutfit, "已识别到内番服时不应输出未显示记录");

naibanOutfitState.Begin();
AssertTrue(naibanOutfitState.ShouldLogMissingOutfit, "对话颜色结束前未识别到内番服时应输出未显示记录");
AssertTrue(naibanOutfitState.TryFinishMissingOutfit(), "未识别到内番服时结束结算应输出一次未显示记录");
AssertFalse(naibanOutfitState.TryFinishMissingOutfit(), "同一轮内番服结束结算不应重复输出未显示记录");

Console.WriteLine("FormationPreset 测试通过。");

static void AssertFalse(bool value, string message)
{
    if (value)
        throw new InvalidOperationException(message);
}

static void AssertTrue(bool value, string message)
{
    if (!value)
        throw new InvalidOperationException(message);
}

static bool InvokeUpdateDataShouldRun(InstanceConfiguration configuration, string interval, DateTime now) =>
    (bool)InvokeStaticMethod("MFAAvalonia.Services.UpdateDataScheduleService", "ShouldRun", configuration, interval, now)!;

static DateTime? InvokeUpdateDataGetLastSucceeded(InstanceConfiguration configuration) =>
    (DateTime?)InvokeStaticMethod("MFAAvalonia.Services.UpdateDataScheduleService", "GetLastSucceeded", configuration);

static void InvokeUpdateDataMarkSucceeded(InstanceConfiguration configuration, DateTime now) =>
    InvokeStaticMethod("MFAAvalonia.Services.UpdateDataScheduleService", "MarkSucceeded", configuration, now);

static bool InvokeTrySaveWarehouseDraft(string draftPath) =>
    (bool)InvokeStaticMethod("MFAAvalonia.Services.UpdateDataPersistenceService", "TrySaveWarehouseDraft", draftPath)!;

static bool InvokeTrySaveSwordBookDraft(string draftPath) =>
    (bool)InvokeStaticMethod("MFAAvalonia.Services.UpdateDataPersistenceService", "TrySaveSwordBookDraft", draftPath)!;

static object? InvokeStaticMethod(string typeName, string methodName, params object?[] arguments)
{
    var assembly = Assembly.GetExecutingAssembly();
    var targetType = assembly.GetType(typeName);
    if (targetType == null)
        throw new InvalidOperationException($"未找到类型 {typeName}");

    var method = targetType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
    if (method == null)
        throw new InvalidOperationException($"未找到方法 {typeName}.{methodName}");

    try
    {
        return method.Invoke(null, arguments);
    }
    catch (TargetInvocationException exception) when (exception.InnerException != null)
    {
        throw exception.InnerException;
    }
}

static List<SwordBookPortraitState> CloneSwordBookStates(IEnumerable<SwordBookPortraitState> states) =>
    [.. states.Select(state => new SwordBookPortraitState(
        state.Number,
        state.Owned,
        state.Wounded,
        state.TrueSword,
        state.InnerCare,
        state.Casual))];

static bool AreSameSwordBookStates(IReadOnlyList<SwordBookPortraitState> left, IReadOnlyList<SwordBookPortraitState> right)
{
    if (left.Count != right.Count)
        return false;

    for (var i = 0; i < left.Count; i++)
    {
        if (left[i].Number != right[i].Number
            || left[i].Owned != right[i].Owned
            || left[i].Wounded != right[i].Wounded
            || left[i].TrueSword != right[i].TrueSword
            || left[i].InnerCare != right[i].InnerCare
            || left[i].Casual != right[i].Casual)
            return false;
    }

    return true;
}

static void DeleteIfExists(string path)
{
    if (File.Exists(path))
        File.Delete(path);
}

static void DeleteDirectoryIfExists(string path)
{
    if (Directory.Exists(path))
        Directory.Delete(path, true);
}
