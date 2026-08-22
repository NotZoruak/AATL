using Avalonia.Controls.Notifications;
using SukiUI.Toasts;
using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Diagnostics;

namespace MFAAvalonia.Helper;

public static class ToastHelper
{
    private const string SurveyUrl = "https://ycnviwngeokc.feishu.cn/share/base/form/shrcnEJvA6mbBOSU2RO7DnRm8Qh";

    public static SukiToastBuilder CreateToastByType(NotificationType toastType, string title = "", object? content = null, int duration = 3)
    {
        if (duration <= 0)
        {
            return Instances.ToastManager.CreateToast()
           .WithTitle(title)
           .WithContent(
               content)
           .OfType(toastType).Dismiss().ByClicking();
        }
        return Instances.ToastManager.CreateToast()
            .WithTitle(title)
            .WithContent(
                content)
            .OfType(toastType).Dismiss().After(TimeSpan.FromSeconds(duration))
            .Dismiss().ByClicking();
    }

    public static void Success(string title = "", object? content = null, int duration = 3)
    {
        DispatcherHelper.RunOnMainThread(() => CreateToastByType(NotificationType.Success, title, content, duration).Queue());
    }

    public static void SuccessWithSurvey(string title, string message, int duration = 0)
    {
        DispatcherHelper.RunOnMainThread(() =>
        {
            var content = new StackPanel { Spacing = 8, MaxWidth = 360 };
            content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

            var surveyButton = new Button
            {
                Content = "去反馈bug",
                Foreground = Brushes.DodgerBlue,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            surveyButton.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(SurveyUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    LoggerHelper.Warning($"打开问卷链接失败：{ex.Message}");
                }
            };
            content.Children.Add(surveyButton);
            CreateToastByType(NotificationType.Success, title, content, duration).Queue();
        });
    }

    public static void Info(string title = "", object? content = null, int duration = 3)
    {
        DispatcherHelper.RunOnMainThread(() => CreateToastByType(NotificationType.Information, title, content, duration).Queue());
    }

    public static void Warn(string title = "", object? content = null, int duration = 3)
    {
        DispatcherHelper.RunOnMainThread(() => CreateToastByType(NotificationType.Warning, title, content, duration).Queue());
    }

    public static void Error(string title = "", object? content = null, int duration = 3)
    {
        DispatcherHelper.RunOnMainThread(() => CreateToastByType(NotificationType.Error, title, content, duration).Queue());
    }
}
