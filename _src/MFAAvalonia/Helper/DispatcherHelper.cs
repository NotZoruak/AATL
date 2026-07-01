using Avalonia;
using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.Helper;

public static class DispatcherHelper
{
    public static void RunOnMainThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Invoke(action);

    }

    public static T RunOnMainThread<T>(Func<T> func)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return func();
        }

        return Dispatcher.UIThread.Invoke(func);

    }
    public static Task RunOnMainThreadAsync(Action action, DispatcherPriority? priority = null, CancellationToken? cancellationToken = null)
    {
        cancellationToken ??= CancellationToken.None;
        priority ??= new DispatcherPriority();
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        // 不在UI线程：异步投放到UI线程，返回可等待的Task
        return Dispatcher.UIThread.InvokeAsync(action, priority.Value, cancellationToken.Value).GetTask();
    }

    public static Task<T> RunOnMainThreadAsync<T>(Func<T> func)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(func());
        }

        return Dispatcher.UIThread.InvokeAsync(func).GetTask();
    }

    public static void PostOnMainThread(Action func, DispatcherPriority priority = default)
    {
        Dispatcher.UIThread.Post(func, priority);
    }
}
