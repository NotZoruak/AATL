using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using System;
using System.IO;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>准备更新数据阶段，清理对应的临时识别草稿。</summary>
public sealed class UpdateDataPrepareAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(UpdateDataPrepareAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            var stage = ActionParamHelper.Parse(args.ActionParam)["stage"]?.ToObject<string>();
            var fileName = stage switch
            {
                "warehouse" => "warehouse_scan.json",
                "swordbook" => "swordbook_scan.json",
                _ => null,
            };
            if (fileName == null)
            {
                LoggerHelper.Error("[更新数据] 准备阶段参数无效");
                return false;
            }

            var path = Path.Combine(AppPaths.ConfigDirectory, fileName);
            if (File.Exists(path))
                File.Delete(path);
            LoggerHelper.Info($"[更新数据] 已准备{(stage == "warehouse" ? "仓库" : "刀帐")}识别");
            return true;
        }
        catch (MaaStopException)
        {
            return false;
        }
        catch (Exception exception)
        {
            LoggerHelper.Error($"[更新数据] 准备识别失败：{exception.Message}");
            return false;
        }
    }
}
