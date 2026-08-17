using MFAAvalonia.Models;

var preset = new FormationPreset();

AssertFalse(preset.ClearEquipmentBeforeFormation, "新预设默认不应卸下现有装备");
AssertFalse(preset.SaveGameFormationRecordAfterFormation, "新预设默认不应保存游戏部队记录");

Console.WriteLine("FormationPreset 测试通过。");

static void AssertFalse(bool value, string message)
{
    if (value)
        throw new InvalidOperationException(message);
}
