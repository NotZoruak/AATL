using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Runtime.InteropServices;

namespace SukiUI.Extensions;

/// <summary>
/// Markup 扩展，用于创建包含系统默认字体的 FontFamily
/// 会根据操作系统自动添加合适的系统字体作为首选字体
/// </summary>
/// <example>
/// XAML 用法:
/// <code>
/// &lt;FontFamily x:Key="DefaultFontFamily"&gt;{ext:SystemFontFamily FallbackFonts='avares://SukiUI/CustomFont#Quicksand'}&lt;/FontFamily&gt;
/// </code>
/// 或者直接使用:
/// <code>
/// &lt;TextBlock FontFamily="{ext:SystemFontFamily FallbackFonts='Arial, Helvetica'}" /&gt;
/// </code>
/// </example>
public class SystemFontFamilyExtension : MarkupExtension
{
    /// <summary>
    /// 后备字体列表，用逗号分隔
    /// 当系统字体不可用时会使用这些字体
    /// </summary>
    public string? FallbackFonts { get; set; }

    /// <summary>
    /// 是否将系统字体放在最前面（默认为 true）
    /// 如果为 false，则系统字体会放在 FallbackFonts 之后
    /// </summary>
    public bool SystemFontFirst { get; set; } = true;

    /// <summary>
    /// 自定义的系统字体（可选）
    /// 如果不设置，会根据操作系统自动选择合适的字体
    /// </summary>
    public string? CustomSystemFont { get; set; }

    public SystemFontFamilyExtension()
    {
    }

    public SystemFontFamilyExtension(string? fallbackFonts)
    {
        FallbackFonts = fallbackFonts;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var systemFont = GetSystemFont();
        var fontList = BuildFontList(systemFont);
        return new FontFamily(fontList);
    }

    /// <summary>
    /// 获取当前操作系统的默认 UI 字体
    /// </summary>
    private string GetSystemFont()
    {
        // 如果用户指定了自定义系统字体，直接使用
        if (!string.IsNullOrWhiteSpace(CustomSystemFont))
        {
            return CustomSystemFont;
        }

        // 根据操作系统返回合适的系统字体
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows 11/10 使用 Segoe UI Variable，旧版本使用 Segoe UI
            // 同时添加中文字体支持
            return "Segoe UI Variable, Segoe UI, Microsoft YaHei UI, Microsoft YaHei";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS 使用 San Francisco 字体（系统字体）
            // -apple-system 是 CSS 中的写法，在 Avalonia 中使用 .AppleSystemUIFont
            // 同时添加中文字体支持
            return ".AppleSystemUIFont, PingFang SC, Hiragino Sans GB";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux 常见的系统字体
            // 同时添加中文字体支持
            return "Ubuntu, Cantarell, DejaVu Sans, Noto Sans CJK SC, WenQuanYi Micro Hei";
        }

        // 其他平台的后备字体
        return "sans-serif";
    }

    /// <summary>
    /// 构建完整的字体列表字符串
    /// </summary>
    private string BuildFontList(string systemFont)
    {
        if (string.IsNullOrWhiteSpace(FallbackFonts))
        {
            return systemFont;
        }

        if (SystemFontFirst)
        {
            return $"{systemFont}, {FallbackFonts}";
        }
        else
        {
            return $"{FallbackFonts}, {systemFont}";
        }
    }
}