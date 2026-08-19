using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using Newtonsoft.Json.Linq;
using System;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class LogAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(LogAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var json = ActionParamHelper.Parse(args.ActionParam);
        var level = (string?)json["level"] ?? "Info";
        var message = (string?)json["message"] ?? string.Empty;

        // click 参数(可选):[x, y, w, h] 四元素,MaaFW target 语义;
        // 在矩形内随机取点点击,与 MaaFW Click action 行为一致
        if (json["click"] is JArray click && click.Count >= 4)
        {
            var w = (int)click[2];
            var h = (int)click[3];
            var x = (int)click[0] + (w > 0 ? Random.Shared.Next(w) : 0);
            var y = (int)click[1] + (h > 0 ? Random.Shared.Next(h) : 0);
            context.Click(x, y);
        }

        if (string.IsNullOrWhiteSpace(message))
            return true;

        // 词表打点只写文件日志(供运行结果页解析),不写 GUI 日志
        if (level == "Warning")
            LoggerHelper.Warning(message);
        else
            LoggerHelper.Info(message);

        return true;
    }
}
