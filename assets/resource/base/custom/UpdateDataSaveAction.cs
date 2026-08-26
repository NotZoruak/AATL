using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using MFAAvalonia.Services;
using System;
using System.IO;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>将更新数据识别草稿提交到对应的正式配置。</summary>
public sealed class UpdateDataSaveAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(UpdateDataSaveAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);
            var stage = ActionParamHelper.Parse(args.ActionParam)["stage"]?.ToObject<string>();
            var saved = stage switch
            {
                "warehouse" => UpdateDataPersistenceService.TrySaveWarehouseDraft(
                    Path.Combine(AppPaths.ConfigDirectory, "warehouse_scan.json")),
                "swordbook" => UpdateDataPersistenceService.TrySaveSwordBookDraft(
                    Path.Combine(AppPaths.ConfigDirectory, "swordbook_scan.json")),
                _ => false,
            };
            if (!saved)
            {
                LoggerHelper.Error($"[更新数据] {(stage == "warehouse" ? "仓库" : "刀帐")}识别结果无效，未覆盖正式数据");
                return false;
            }

            LoggerHelper.Info($"[更新数据] {(stage == "warehouse" ? "仓库" : "刀帐")}数据已保存");
            return true;
        }
        catch (MaaStopException)
        {
            return false;
        }
        catch (Exception exception)
        {
            LoggerHelper.Error($"[更新数据] 保存识别结果失败：{exception.Message}");
            return false;
        }
    }
}
