using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using AvaloniaExtensions.Axaml.Markup;
using Lang.Avalonia.MarkupExtensions;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using System;
using System.Threading.Tasks;


namespace MFAAvalonia.Views.UserControls.Settings;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
public partial class VersionUpdateSettingsUserControl : UserControl
{
    public VersionUpdateSettingsUserControl()
    {
        InitializeComponent();
        DataContext = Instances.VersionUpdateSettingsUserControlModel;
        Instances.VersionUpdateSettingsUserControlModel.RefreshDebugActionsVisibility();
        ApplyDebugVisibility();
    }

    private void ApplyDebugVisibility()
    {
        try
        {
            DebugLocalPackageCard.IsVisible = Instances.VersionUpdateSettingsUserControlModel.ShowLocalPackageUpdate;
            LoggerHelper.Info($"设置页调试卡片可见性已应用：{DebugLocalPackageCard.IsVisible}");
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"设置页调试卡片可见性应用失败：{ex.Message}");
        }
    }

    private void CopyVersion(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock textBlock)
        {
            var version = textBlock.Text;
            if (!string.IsNullOrEmpty(version))
            {
                TaskManager.RunTaskAsync(async () =>
                {
                    var clipboard = Instances.Clipboard;
                    if (clipboard == null)
                    {
                        ToastHelper.Warn(LangKeys.Warning.ToLocalization(), LangKeys.PlatformNotSupportedOperation.ToLocalization());
                        return;
                    }

                    DispatcherHelper.PostOnMainThread(async () => await clipboard.SetTextAsync(version));
                    DispatcherHelper.PostOnMainThread(() => textBlock.Bind(ToolTip.TipProperty, new I18nBinding(LangKeys.CopiedToClipboard)));
                    DispatcherHelper.PostOnMainThread(() => ToolTip.SetIsOpen(textBlock, true));
                    await Task.Delay(1000);
                    DispatcherHelper.PostOnMainThread(() => ToolTip.SetIsOpen(textBlock, false));
                    DispatcherHelper.PostOnMainThread(() => textBlock.Bind(ToolTip.TipProperty, new I18nBinding(LangKeys.CopyToClipboard)));
                }, name: "复制版本号");
            }
        }
    }
}
