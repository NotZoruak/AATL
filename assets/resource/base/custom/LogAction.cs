using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class LogAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(LogAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        var json = ActionParamHelper.Parse(args.ActionParam);
        var level = (string?)json["level"] ?? "Info";
        var message = (string?)json["message"] ?? string.Empty;
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
