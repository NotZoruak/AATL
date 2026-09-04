using Avalonia.Controls.Notifications;
using SukiUI.Toasts;
using System;

namespace MFAAvalonia.Helper;

public static class ToastHelper
{
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
