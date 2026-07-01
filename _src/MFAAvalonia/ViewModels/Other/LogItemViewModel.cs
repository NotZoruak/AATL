using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using SukiUI;
using System;
using System.Text.RegularExpressions;

namespace MFAAvalonia.ViewModels.Other;

/// <summary>
/// 日志项 ViewModel - 实现 IDisposable 以正确释放事件订阅，避免内存泄漏
/// </summary>
public partial class LogItemViewModel : ViewModelBase, IDisposable
{
    private readonly string[]? _formatArgsKeys;
    private readonly bool _transformKey = true;
    private readonly bool _changeColor = true;
    // 存储基础颜色值而不是 IBrush 引用，避免持有对资源系统的引用
    private readonly Color? _baseColor;
    private readonly bool _useKey;
    private bool _disposed;
    //缓存事件处理器委托，确保订阅和取消订阅使用同一个委托实例
    private readonly Action<ThemeVariant>? _themeChangedHandler;
    private readonly EventHandler<LanguageHelper.LanguageEventArgs>? _languageChangedHandler;

    public LogItemViewModel(string resourceKey,
        IBrush color,
        string weight = "Regular",
        bool useKey = false,
        string dateFormat = "MM'-'dd'  'HH':'mm':'ss",
        bool changeColor = true,
        bool showTime = true,
        params string[] formatArgsKeys)
    {
        _resourceKey = resourceKey;
        _useKey = useKey;

        Time = DateTime.Now.ToString(dateFormat);
        _changeColor = changeColor;
        Weight = weight;
        ShowTime = showTime;
        // 提取颜色值而不是存储 IBrush 引用
        _baseColor = (color as ISolidColorBrush)?.Color;

        if (_changeColor)
        {
            _themeChangedHandler = OnThemeChanged;
            SukiTheme.GetInstance().OnBaseThemeChanged += _themeChangedHandler;
        }
        OnThemeChanged(Instances.GuiSettingsUserControlModel.BaseTheme);
        if (useKey)
        {
            _formatArgsKeys = formatArgsKeys;
            UpdateContent();
            _languageChangedHandler = OnLanguageChanged;
            LanguageHelper.LanguageChanged += _languageChangedHandler;
        }
        else
            Content = resourceKey;
    }

    public LogItemViewModel(string resourceKey,
        IBrush color,
        string weight = "Regular",
        bool useKey = false,
        string dateFormat = "MM'-'dd'  'HH':'mm':'ss",
        bool changeColor = true,
        bool showTime = true,
        bool transformKey = true,
        params string[] formatArgsKeys)
    {
        _resourceKey = resourceKey;
        _transformKey = transformKey;
        _useKey = useKey;
        Time = DateTime.Now.ToString(dateFormat);
        _changeColor = changeColor;
        Weight = weight;
        ShowTime = showTime;
        // 提取颜色值而不是存储 IBrush 引用
        _baseColor = (color as ISolidColorBrush)?.Color;
        // 先设置颜色
        if (_baseColor.HasValue)
            Color = new SolidColorBrush(_baseColor.Value);
        else
            Color = color;

        if (_changeColor)
        {
            _themeChangedHandler = OnThemeChanged;
            SukiTheme.GetInstance().OnBaseThemeChanged += _themeChangedHandler;
        }
        OnThemeChanged(Instances.GuiSettingsUserControlModel.BaseTheme);
        if (useKey)
        {
            _formatArgsKeys = formatArgsKeys;
            UpdateContent();
            _languageChangedHandler = OnLanguageChanged;
            LanguageHelper.LanguageChanged += _languageChangedHandler;
        }
        else
            Content = resourceKey;
    }

    public LogItemViewModel(string content,
        IBrush? color,
        string weight = "Regular",
        string dateFormat = "MM'-'dd'  'HH':'mm':'ss",
        bool showTime = true,
        bool changeColor = true)
    {
        Time = DateTime.Now.ToString(dateFormat);
        // 提取颜色值而不是存储 IBrush 引用
        _baseColor = (color as ISolidColorBrush)?.Color;
        _changeColor = changeColor;

        if (_changeColor)
        {
            _themeChangedHandler = OnThemeChanged;
            SukiTheme.GetInstance().OnBaseThemeChanged += _themeChangedHandler;
        }
        OnThemeChanged(Instances.GuiSettingsUserControlModel.BaseTheme);
        Weight = weight;
        ShowTime = showTime;
        Content = content;
    }

    private string _time;

    public string Time
    {
        get => _time;
        set => SetProperty(ref _time, value);
    }

    private bool _showTime = true;

    public bool ShowTime
    {
        get => _showTime;
        set => SetProperty(ref _showTime, value);
    }

    private string _content;

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    private IBrush _color;

    public IBrush Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    public IBrush BackgroundColor
    {
        get;
        set => SetProperty(ref field, value);
    } = Brushes.Transparent;

    private string _weight = "Regular";

    public string Weight
    {
        get => _weight;
        set => SetProperty(ref _weight, value);
    }

    private string _resourceKey;

    public string ResourceKey
    {
        get => _resourceKey;
        set
        {
            if (SetProperty(ref _resourceKey, value))
            {
                UpdateContent();
            }
        }
    }

    private void UpdateContent()
    {
        if (_formatArgsKeys == null || _formatArgsKeys.Length == 0)
            Content = ResourceKey.ToLocalization();
        else
        {
            try
            {
                Content = Regex.Unescape(
                    _resourceKey.ToLocalizationFormatted(_transformKey, _formatArgsKeys));
            }
            catch
            {
                Content = _resourceKey.ToLocalizationFormatted(_transformKey, _formatArgsKeys);
            }
        }
    }
    private bool _isDownloading;

    public bool IsDownloading
    {
        get => _isDownloading;
        set => SetProperty(ref _isDownloading, value);
    }

    private bool _useMarkdown;

    /// <summary>
    /// 是否使用Markdown渲染内容，默认关闭
    /// </summary>
    public bool UseMarkdown
    {
        get => _useMarkdown;
        set => SetProperty(ref _useMarkdown, value);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateContent();
    }

    private void OnThemeChanged(ThemeVariant variant)
    {
        if (_disposed) return;

        if (!_changeColor || variant == ThemeVariant.Light)
        {
            if (_baseColor.HasValue)
                Color = new SolidColorBrush(_baseColor.Value);
        }
        else
        {
            if (_baseColor.HasValue)
            {
                // 反转颜色
                var invertedColor = new Color(
                    _baseColor.Value.A,
                    (byte)(255 - _baseColor.Value.R),
                    (byte)(255 - _baseColor.Value.G),
                    (byte)(255 - _baseColor.Value.B));
                Color = new SolidColorBrush(invertedColor);
            }
        }
    }

    /// <summary>
    /// 释放事件订阅，防止内存泄漏
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            // 使用缓存的委托实例取消订阅，确保能正确移除
            if (_languageChangedHandler != null)
            {
                LanguageHelper.LanguageChanged -= _languageChangedHandler;
            }

            if (_themeChangedHandler != null)
            {
                try
                {
                    SukiTheme.GetInstance().OnBaseThemeChanged -= _themeChangedHandler;
                }
                catch
                {
                    // 忽略异常，可能主题实例已被释放
                }
            }
            // 清理颜色引用
            Color = null!;
            BackgroundColor = null!;
        }
    }

    ~LogItemViewModel()
    {
        Dispose(false);
    }
}
