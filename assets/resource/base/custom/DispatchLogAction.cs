using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class DispatchLogAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(DispatchLogAction);

    private static int? _markedTeam;

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var json = ActionParamHelper.Parse(args.ActionParam);
        var mode = (string)json["mode"];

        if (mode == "mark")
        {
            int team = (int?)json["team"] ?? 0;
            _markedTeam = team;
            return true;
        }

        if (mode == "log")
        {
            int team = _markedTeam ?? 0;
            _markedTeam = null;
            string message;

            if (team == 0)
            {
                message = "[远征派遣] 派出远征队伍";
            }
            else
            {
                string teamLabel = TeamToLabel(team);
                string mapLabel = ReadMapFromConfig(team);
                message = $"[远征派遣] {teamLabel}已派遣至 {mapLabel}";
            }

            Log(message);
            return true;
        }

        return true;
    }

    private static void Log(string message)
    {
        LoggerHelper.Info(message);
        try
        {
            MaaProcessorManager.Instance.Current?.AddLog(message);
        }
        catch
        {
            // 静默忽略，确保不影响流水线执行
        }
    }

    private static string TeamToLabel(int team) => $"部队{team}";

    private static string TeamToConfigName(int team) => team switch
    {
        1 => "部队一",
        2 => "部队二",
        3 => "部队三",
        4 => "部队四",
        5 => "部队五",
        _ => $"部队{team}"
    };

    private static string ReadMapFromConfig(int team)
    {
        try
        {
            string instancesDir = AppPaths.InstancesDirectory;
            if (!Directory.Exists(instancesDir))
                return "??";

            // 使用当前激活实例的 UUID 定位配置文件（config/instances/{uuid}.json），
            // 与 InstanceConfiguration.GetConfigFilePath() 保持一致。
            // 注意：不能使用 appsettings.json 的 Instances.LastActiveName（实例显示名），
            // 显示名与实例文件名无关；且回退 default.json 会读错其他实例的远征配置。
            string instanceId = MaaProcessorManager.Instance?.Current?.InstanceId ?? string.Empty;
            string configPath = string.IsNullOrWhiteSpace(instanceId)
                ? Path.Combine(instancesDir, "default.json")
                : Path.Combine(instancesDir, $"{instanceId}.json");
            if (!File.Exists(configPath))
                configPath = Path.Combine(instancesDir, "default.json");
            if (!File.Exists(configPath))
                return "??";

            var config = JObject.Parse(File.ReadAllText(configPath));
            var taskItems = config["TaskItems"] as JArray;
            if (taskItems == null)
                return "??";

            string teamName = TeamToConfigName(team);
            foreach (var item in taskItems)
            {
                if ((string)item["name"] == "远征")
                {
                    var options = item["option"] as JArray;
                    if (options != null)
                    {
                        foreach (var opt in options)
                        {
                            if ((string)opt["name"] == teamName)
                            {
                                int mapIndex = (int)opt["index"];
                                if (mapIndex <= 0)
                                    return "休息";
                                int era = (mapIndex - 1) / 4 + 1;
                                int region = (mapIndex - 1) % 4 + 1;
                                return $"{era}-{region}";
                            }
                        }
                    }
                    break;
                }
            }
            return "??";
        }
        catch
        {
            return "??";
        }
    }
}
