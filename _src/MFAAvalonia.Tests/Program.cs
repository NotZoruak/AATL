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

var name = SavedWorkRecordService.CreateUniqueName("地下城", ["地下城", "地下城（1）"]);
AssertTrue(name == "地下城（2）", "重名保存记录应自动追加递增编号");

var savedPath = Path.Combine(Path.GetTempPath(), $"matr-saved-{Guid.NewGuid():N}.json");
SavedWorkRecordStore.Save(savedPath, [merged]);
var loaded = SavedWorkRecordStore.Load(savedPath);
File.Delete(savedPath);
AssertTrue(loaded.Count == 1 && loaded[0].DisplayName == "地下城周回", "保存记录应能从本地文件恢复");

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
