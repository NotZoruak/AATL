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
