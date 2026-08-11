using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class ExpeditionMapSelectAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(ExpeditionMapSelectAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            var json = ActionParamHelper.Parse(args.ActionParam);
            int team = (int?)json["team"] ?? 1;
            string teamLabel = TeamToLabel(team);

            // 读取实例配置文件（默认使用当前激活的实例）
            string instancesDir = AppPaths.InstancesDirectory;
            if (!Directory.Exists(instancesDir))
            {
                LoggerHelper.Error($"[ExpeditionMapSelect] 实例目录不存在: {instancesDir}");
                return false;
            }

            // 使用当前激活实例的 UUID 定位配置文件（config/instances/{uuid}.json），
            // 与 InstanceConfiguration.GetConfigFilePath() 保持一致。
            // 注意：不能使用 appsettings.json 的 Instances.LastActiveName（实例显示名），
            // 显示名与实例文件名无关；且回退 default.json 会读错其他实例的远征配置。
            string instanceId = MaaProcessorManager.Instance?.Current?.InstanceId ?? string.Empty;
            string configPath = string.IsNullOrWhiteSpace(instanceId)
                ? Path.Combine(instancesDir, "default.json")
                : Path.Combine(instancesDir, $"{instanceId}.json");
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(instancesDir, "default.json");
            }
            if (!File.Exists(configPath))
            {
                LoggerHelper.Error($"[ExpeditionMapSelect] 找不到实例配置文件");
                return false;
            }

            var config = JObject.Parse(File.ReadAllText(configPath));
            var taskItems = config["TaskItems"] as JArray;
            if (taskItems == null)
            {
                LoggerHelper.Error("[ExpeditionMapSelect] TaskItems 不存在");
                return false;
            }

            // 从「远征」任务配置中找到该部队的地图选择
            int mapIndex = -1;
            foreach (var item in taskItems)
            {
                if ((string)item["name"] == "远征")
                {
                    var options = item["option"] as JArray;
                    if (options != null)
                    {
                        foreach (var opt in options)
                        {
                            if ((string)opt["name"] == teamLabel)
                            {
                                mapIndex = (int)opt["index"];
                                break;
                            }
                        }
                    }
                    break;
                }
            }

            if (mapIndex <= 0) // index=0 是「休息」
            {
                LoggerHelper.Info($"[ExpeditionMapSelect] {teamLabel} 设置为休息，跳过派遣");
                return false;
            }

            // index 映射: 1=1-1, 2=1-2, 3=1-3, 4=1-4, 5=2-1, ..., 20=5-4
            int era = (mapIndex - 1) / 4 + 1;
            int region = (mapIndex - 1) % 4 + 1;
            string mapLabel = $"{era}-{region}";

            // 点击时代标签
            int[] eraTarget = GetEraTarget(era);
            int eraX = eraTarget[0] + eraTarget[2] / 2;
            int eraY = eraTarget[1] + eraTarget[3] / 2;
            LoggerHelper.Info($"[ExpeditionMapSelect] {teamLabel} → {mapLabel}，点击时代{era} ({eraX}, {eraY})");
            context.Click(eraX, eraY);
            ActionParamHelper.SleepWithStopCheck(context, 300);

            // 点击小地图区域
            int[] regionTarget = GetRegionTarget(region);
            int regionX = regionTarget[0] + regionTarget[2] / 2;
            int regionY = regionTarget[1] + regionTarget[3] / 2;
            LoggerHelper.Info($"[ExpeditionMapSelect] {teamLabel} → {mapLabel}，点击地域{region} ({regionX}, {regionY})");
            context.Click(regionX, regionY);

            LoggerHelper.Info($"[ExpeditionMapSelect] {teamLabel} 地图选择完成: {mapLabel}");
            return true;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("[ExpeditionMapSelect] 手动停止");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"[ExpeditionMapSelect] 错误: {e.Message}");
            return false;
        }
    }

    private static string TeamToLabel(int team) => team switch
    {
        1 => "部队一",
        2 => "部队二",
        3 => "部队三",
        4 => "部队四",
        5 => "部队五",
        _ => $"部队{team}"
    };

    /// <summary>
    /// 获取时代标签的点击坐标，与 interface.json 中部队一的各时代 target 保持一致
    /// </summary>
    private static int[] GetEraTarget(int era) => era switch
    {
        1 => new[] { 276, 188, 37, 39 },
        2 => new[] { 469, 178, 50, 40 },
        3 => new[] { 661, 183, 50, 35 },
        4 => new[] { 891, 190, 50, 36 },
        5 => new[] { 1078, 191, 45, 34 },
        _ => new[] { 276, 188, 37, 39 }
    };

    /// <summary>
    /// 获取小地图区域的点击坐标，与 interface.json 中各地域的 target 保持一致
    /// </summary>
    private static int[] GetRegionTarget(int region) => region switch
    {
        1 => new[] { 173, 365, 97, 96 },
        2 => new[] { 487, 373, 84, 113 },
        3 => new[] { 767, 370, 81, 115 },
        4 => new[] { 1098, 368, 67, 79 },
        _ => new[] { 173, 365, 97, 96 }
    };
}
