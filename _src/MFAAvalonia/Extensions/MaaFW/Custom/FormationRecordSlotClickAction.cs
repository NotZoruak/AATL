using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using Newtonsoft.Json.Linq;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>部队记录页面：按目标部队点击同编号记录槽</summary>
public class FormationRecordSlotClickAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(FormationRecordSlotClickAction);

    /// <summary>记录槽位置（1280×720 基准，与部队同编号）</summary>
    private static readonly int[][] RecordSlotCoords =
    [
        [1210, 144, 29, 70],
        [1211, 255, 30, 71],
        [1212, 361, 30, 71],
        [1211, 470, 30, 71],
        [1210, 577, 30, 71],
    ];

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var json = ActionParamHelper.Parse(args.ActionParam);
            int team = json["team"]?.Value<int>() ?? FormationContext.Team;
            if (team < 1 || team > 5)
            {
                LoggerHelper.Error($"[FormationRecordSlot] 部队编号非法: {team}");
                return false;
            }

            var coords = RecordSlotCoords[team - 1];
            LoggerHelper.Info($"[FormationRecordSlot] 选择记录槽（部队{team}）→ 点击 [{string.Join(",", coords)}]");
            int cx = coords[0] + coords[2] / 2;
            int cy = coords[1] + coords[3] / 2;
            context.Click(cx, cy);
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[FormationRecordSlot] 手动停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[FormationRecordSlot] Error: {e.Message}");
            return false;
        }
    }
}
