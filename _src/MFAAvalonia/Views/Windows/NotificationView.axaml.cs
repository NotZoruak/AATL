using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using MFAAvalonia.Helper;
using SukiUI.Controls;
using SukiUI.Extensions;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.Views.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsPInvoke
{
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint SPI_SETWORKAREA = 0x002F;
    public const uint SPI_GETWORKAREA = 0x0030;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public extern static bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rect ToAvaloniaRect()
        {
            return new Rect(Left, Top, Right - Left, Bottom - Top);
        }
    }
}

public partial class NotificationView : SukiWindow
{
    public double ActualToastHeight { get; private set; }

    public event Action? OnActionButtonClicked;

    public static readonly StyledProperty<bool> HasActionButtonProperty =
        AvaloniaProperty.Register<NotificationView, bool>(nameof(HasActionButton), false);

    public bool HasActionButton
    {
        get => GetValue(HasActionButtonProperty);
        set => SetValue(HasActionButtonProperty, value);
    }

    public static readonly StyledProperty<object?> TitleTextProperty =
        AvaloniaProperty.Register<NotificationView, object?>(nameof(TitleText), "Title");

    public object? TitleText
    {
        get => GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public static readonly StyledProperty<object?> MessageTextProperty =
        AvaloniaProperty.Register<NotificationView, object?>(nameof(MessageText), "Content");

    public object? MessageText
    {
        get => GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    public static readonly StyledProperty<object?> ActionButtonContentProperty =
        AvaloniaProperty.Register<NotificationView, object?>(nameof(ActionButtonContent), "");

    public object? ActionButtonContent
    {
        get => GetValue(ActionButtonContentProperty);
        set => SetValue(ActionButtonContentProperty, value);
    }

    private Timer? _autoCloseTimer;
    private readonly TimeSpan _timeout;
    private bool _isClosed;
    private bool _isClosing;

    public bool IsClosed => _isClosed;
    public bool IsClosing => _isClosing;

    public NotificationView(long duration)
    {
        DataContext = this;
        InitializeComponent();
        _timeout = TimeSpan.FromMilliseconds(duration);
        Opened += OnOpened;
        LayoutUpdated += OnLayoutUpdated;
        ActionButton.Click += OnActionButtonClick;
    }

    public NotificationView() : this(2000)
    {
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // 延迟一帧后开始动画，确保布局完成
        DispatcherHelper.PostOnMainThread(CalculateInitialPositionAndStartAnimation
            , DispatcherPriority.Loaded);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Bounds.Height > 0)
        {
            var screen = this.GetHostScreen();
            if (screen == null) return;
            ActualToastHeight = Bounds.Height * screen.Scaling;
        }
    }

    private void OnActionButtonClick(object? sender, RoutedEventArgs e)
    {
        OnActionButtonClicked?.Invoke();
        StopAutoCloseTimer();
        _ = CloseWithAnimation();
    }

    public PixelRect GetLatestWorkArea(Screen screen)
    {
        return screen.WorkingArea;
    }

    private void CalculateInitialPositionAndStartAnimation()
    {
        var screen = this.GetHostScreen();
        if (screen == null) return;

        double scaling = screen.Scaling;
        double physicalWidth = Bounds.Width * scaling;
        double physicalHeight = Bounds.Height * scaling;
        ActualToastHeight = physicalHeight;

        var workArea = GetLatestWorkArea(screen);
        var targetX = (int)(workArea.Right - physicalWidth - ToastNotification.MarginRight * scaling);
        var targetY = (int)(workArea.Bottom - physicalHeight - ToastNotification.MarginBottom * scaling);

        // 初始位置：在屏幕底部外（准备从底部滑入）
        var startY = (int)workArea.Bottom;
        Position = new PixelPoint(targetX, startY);

        // 开始滑入动画
        _ = StartSlideInAnimation(new PixelPoint(targetX, targetY));
    }

    public void SetContent(object? title, object? message)
    {
        TitleText = title;
        MessageText = message;
    }

    public void SetActionButton(string text, Action onClick)
    {
        ActionButtonContent = text;
        HasActionButton = true;
        OnActionButtonClicked += onClick;
    }

    /// <summary>
    /// 移动到目标位置（使用 Position 动画）
    /// </summary>
    public async Task MoveToAsync(PixelPoint targetPosition, TimeSpan duration)
    {
        if (_isClosed || _isClosing) return;

        var startPosition = Position;
        if (startPosition == targetPosition) return;

        if (duration.TotalMilliseconds <= 0)
        {
            Position = targetPosition;
            return;
        }

        // 使用简单的插值动画
        var startTime = DateTime.Now;
        var totalMs = duration.TotalMilliseconds;

        while (!_isClosed && !_isClosing)
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            var progress = Math.Min(elapsed / totalMs, 1.0);

            // 使用 CubicEaseOut 缓动
            var easedProgress = 1 - Math.Pow(1 - progress, 3);

            var currentX = (int)(startPosition.X + (targetPosition.X - startPosition.X) * easedProgress);
            var currentY = (int)(startPosition.Y + (targetPosition.Y - startPosition.Y) * easedProgress);

            await DispatcherHelper.RunOnMainThreadAsync(() =>
            {
                if (!_isClosed && !_isClosing)
                    Position = new PixelPoint(currentX, currentY);
            });

            if (progress >= 1.0) break;
            await Task.Delay(16); // ~60fps
        }

        // 确保最终位置准确
        if (!_isClosed && !_isClosing)
        {
            await DispatcherHelper.RunOnMainThreadAsync(() => Position = targetPosition);
        }
    }

    /// <summary>
    /// 同步版本的移动方法（供 ToastNotification 调用）
    /// </summary>
    public void MoveTo(PixelPoint targetPosition, TimeSpan duration, Action? onComplete = null)
    {
        if (_isClosed || _isClosing) return;

        _ = Task.Run(async () =>
        {
            await MoveToAsync(targetPosition, duration);
            if (onComplete != null)
            {
                await DispatcherHelper.RunOnMainThreadAsync(onComplete);
            }
        });
    }

    async private Task StartSlideInAnimation(PixelPoint targetPosition)
    {
        // 窗口从底部滑入（使用 Position 动画）
        var startPosition = Position;
        var startTime = DateTime.Now;
        var duration = TimeSpan.FromMilliseconds(250);

        while (!_isClosed && !_isClosing)
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            var progress = Math.Min(elapsed / duration.TotalMilliseconds, 1.0);

            // CubicEaseOut 缓动
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            var currentY = (int)(startPosition.Y + (targetPosition.Y - startPosition.Y) * easedProgress);

            await DispatcherHelper.RunOnMainThreadAsync(() =>
            {
                if (!_isClosed && !_isClosing)
                {
                    Position = new PixelPoint(targetPosition.X, currentY);
                }
            });

            if (progress >= 1.0) break;
            await Task.Delay(16); // ~60fps
        }

        // 确保最终位置准确
        if (!_isClosed && !_isClosing)
        {
            await DispatcherHelper.RunOnMainThreadAsync(() => Position = targetPosition);
        }

        StartAutoCloseTimer();
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        StopAutoCloseTimer();
        _ = CloseWithAnimation();
    }

    async private Task CloseWithAnimation()
    {
        if (_isClosing || _isClosed) return;
        _isClosing = true;

        // 向右滑出（使用 Position 动画）
        var screen = this.GetHostScreen();
        if (screen != null)
        {
            var startPosition = Position;
            var targetX = (int)screen.WorkingArea.Right; // 滑出到屏幕右侧外
            var startTime = DateTime.Now;
            var duration = TimeSpan.FromMilliseconds(200);

            while (!_isClosed)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var progress = Math.Min(elapsed / duration.TotalMilliseconds, 1.0);

                // CubicEaseIn 缓动
                var easedProgress = Math.Pow(progress, 3);
                var currentX = (int)(startPosition.X + (targetX - startPosition.X) * easedProgress);

                await DispatcherHelper.RunOnMainThreadAsync(() =>
                {
                    if (!_isClosed)
                    {
                        Position = new PixelPoint(currentX, startPosition.Y);
                    }
                });

                if (progress >= 1.0) break;
                await Task.Delay(16); // ~60fps
            }
        }
        else
        {
            // 等待淡出动画完成后关闭
            await Task.Delay(200);
        }

        Close();
    }

    private void StartAutoCloseTimer()
    {
        _autoCloseTimer = new Timer(o =>
        {
            DispatcherHelper.PostOnMainThread(() => _ = CloseWithAnimation());
        }, null, _timeout, Timeout.InfiniteTimeSpan);
    }

    private void StopAutoCloseTimer()
    {
        _autoCloseTimer?.Dispose();
        _autoCloseTimer = null;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _isClosed = true;
        StopAutoCloseTimer();
        base.OnClosing(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private extern static int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [SupportedOSPlatform("windows")]
    private extern static int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EX_STYLE = -20;
    private const int WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (OperatingSystem.IsWindows())
        {
            HookWndProcForWorkAreaChange();
            SetWindowHideFromTaskSwitcher();
        }
    }

    [SupportedOSPlatform("windows")]
    private void HookWndProcForWorkAreaChange()
    {
        try
        {
            var topLevel = GetTopLevel(this);
            var handle = topLevel?.TryGetPlatformHandle()?.Handle;
            if (handle == null || handle == IntPtr.Zero)
            {
                LoggerHelper.Warning("无法获取窗口句柄，无法监听工作区变化");
                return;
            }

            Win32Properties.AddWndProcHookCallback(topLevel, (hwnd, msg, wParam, lParam, ref handled) =>
            {
                if (msg == WindowsPInvoke.WM_SETTINGCHANGE && (uint)wParam == WindowsPInvoke.SPI_SETWORKAREA)
                {
                    DispatcherHelper.PostOnMainThread(() =>
                    {
                        ToastNotification.Instance.UpdateAllToastPositions();
                    }, DispatcherPriority.Background);

                    handled = true;
                }
                return IntPtr.Zero;
            });
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"注册窗口钩子失败: {ex.Message}", ex);
        }
    }

    private void SetWindowHideFromTaskSwitcher()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel?.TryGetPlatformHandle()?.Handle is not IntPtr handle || handle == IntPtr.Zero)
        {
            LoggerHelper.Warning("无法获取窗口平台句柄");
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                SetWindowsWindowStyle(handle);
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"设置窗口隐藏属性失败: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private void SetWindowsWindowStyle(IntPtr handle)
    {
        int currentStyle = GetWindowLong(handle, GWL_EX_STYLE);
        int newStyle = (currentStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLong(handle, GWL_EX_STYLE, newStyle);
    }
}
