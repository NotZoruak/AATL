using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using Newtonsoft.Json.Linq;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>装备解除面板：按目标部队点击对应部队按钮</summary>
public class FormationEquipTeamClickAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(FormationEquipTeamClickAction);

    /// <summary>装备解除面板部队按钮位置（1280×720 基准）</summary>
    private static readonly int[][] TeamCoords =
    [
        [348, 387, 59, 29],
        [526, 388, 59, 29],
        [701, 390, 59, 29],
        [875, 387, 59, 29],
        [351, 458, 60, 30],
    ];

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var json = ActionParamHelper.Parse(args.ActionParam);
            int team = json["team"]?.Value<int>() ?? FormationContext.Team;
            if (team < 1 || team > 5)
            {
                LoggerHelper.Error($"[FormationEquipTeam] 部队编号非法: {team}");
                return false;
            }

            var coords = TeamCoords[team - 1];
            LoggerHelper.Info($"[FormationEquipTeam] 选择部队{team} → 点击 [{string.Join(",", coords)}]");
            int cx = coords[0] + coords[2] / 2;
            int cy = coords[1] + coords[3] / 2;
            context.Click(cx, cy);
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[FormationEquipTeam] 手动停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[FormationEquipTeam] Error: {e.Message}");
            return false;
        }
    }
}
