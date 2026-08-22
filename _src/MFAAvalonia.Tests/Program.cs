using MFAAvalonia.Models;
using MFAAvalonia.Services;
using System.Collections.Generic;

var preset = new FormationPreset();

AssertFalse(preset.ClearEquipmentBeforeFormation, "新预设默认不应卸下现有装备");
AssertFalse(preset.SaveGameFormationRecordAfterFormation, "新预设默认不应保存游戏部队记录");

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

var runningRecord = WorkRecordBuilder.Build([
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "INF", "[地下城] 点击行军"),
]);
AssertTrue(runningRecord.Count == 1, "运行中的任务应保留为一条工作记录");
AssertTrue(runningRecord[0].Status == "进行中", "没有停止状态的运行记录应显示进行中");

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
    new LogEntry(logStart, "INF", "开始任务：地下城"),
    new LogEntry(logStart.AddSeconds(1), "INF", "开始任务：后勤"),
    new LogEntry(logStart.AddSeconds(2), "INF", "[地下城] 刀剑掉落 短刀 秋田藤四郎"),
    new LogEntry(logStart.AddSeconds(3), "INF", "[后勤] 派遣远征 部队1已派遣至 2-4"),
    new LogEntry(logStart.AddSeconds(4), "INF", "停止前状态：SUCCEEDED"),
]);
var parallelDungeon = parallelTaskRecords.Single(record => record.TaskName == "地下城");
var parallelLogistics = parallelTaskRecords.Single(record => record.TaskName == "后勤");
AssertTrue(parallelDungeon.SwordDrops.Count == 1, "并行任务中地下城掉落应归入地下城记录");
AssertTrue(parallelLogistics.SwordDrops.Count == 0, "并行任务中后勤记录不应包含地下城掉落");

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
