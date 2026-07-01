using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lang.Avalonia.MarkupExtensions;
using MFAAvalonia.Helper;
using SukiUI.Controls;
using System;
using System.Threading.Tasks;

namespace MFAAvalonia.Views.Windows;

public partial class LicenseView : SukiWindow
{
    public static readonly StyledProperty<string?> LicenseContentProperty =
        AvaloniaProperty.Register<LicenseView, string?>(nameof(LicenseContent), string.Empty);

    public string? LicenseContent
    {
        get => GetValue(LicenseContentProperty);
        set => SetValue(LicenseContentProperty, value);
    }

    public LicenseView()
    {
        DataContext = this;
        InitializeComponent();
    }

    public LicenseView(string licenseContent) : this()
    {
        LicenseContent = licenseContent;
    }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    public static void ShowLicense(string licenseContent, Window? owner = null)
    {
        DispatcherHelper.RunOnMainThread(() =>
        {
            var licenseView = new LicenseView(licenseContent);
            licenseView.ShowDialog(owner ?? Instances.RootView);
        });
    }
#pragma warning restore CS4014

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void CopyLicense_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(LicenseContent))
            return;

        TaskManager.RunTask(async () =>
        {
            DispatcherHelper.PostOnMainThread(async () => await Clipboard.SetTextAsync(LicenseContent));

            // 显示提示
            if (sender is Control control)
            {
                DispatcherHelper.PostOnMainThread(() => control.Bind(ToolTip.TipProperty, new Lang.Avalonia.MarkupExtensions.I18nBinding(LangKeys.CopiedToClipboard)));
                DispatcherHelper.PostOnMainThread(() => ToolTip.SetIsOpen(control, true));
                await Task.Delay(1000);
                DispatcherHelper.PostOnMainThread(() => ToolTip.SetIsOpen(control, false));
                DispatcherHelper.PostOnMainThread(() => control.Bind(ToolTip.TipProperty, new Lang.Avalonia.MarkupExtensions.I18nBinding(LangKeys.CopyToClipboard)));
            }
        }, name: "复制许可证内容到剪贴板");
    }
}
