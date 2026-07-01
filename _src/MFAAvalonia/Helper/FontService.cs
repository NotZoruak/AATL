using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MFAAvalonia.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Helper;

/// <summary>
/// 字体服务类,用于管理全局字体缩放和字体选择
/// </summary>
public partial class FontService : ObservableObject
{
    /// <summary>
    /// 单例实例
    /// </summary>
    public static FontService Instance { get; } = new();

    /// <summary>
    /// 默认缩放比例
    /// </summary>
    public const double DefaultScale = 1.0;

    /// <summary>
    /// 最小缩放比例
    /// </summary>
    public const double MinScale = 0.8;

    /// <summary>
    /// 最大缩放比例
    /// </summary>
    public const double MaxScale = 1.5;

    /// <summary>
    /// 当前字体缩放比例
    /// </summary>
    [ObservableProperty]
    private double _currentScale = DefaultScale;

    /// <summary>
    /// 用于UI缩放的 ScaleTransform（缓存以避免频繁创建）
    /// </summary>
    [ObservableProperty]
    private ScaleTransform _scaleTransform = new(1, 1);

    /// <summary>
    /// 基础字体大小定义（与SukiUI 默认值对应）
    /// </summary>
    private static readonly Dictionary<string, double> BaseFontSizes = new()
    {
        ["FontSizeSmall"] = 12,
        ["FontSizeMedium"] = 14,
        ["FontSizeLarge"] = 16,
        ["FontSizeH1"] = 32,
        ["FontSizeH2"] = 28,
        ["FontSizeH3"] = 24,
        ["FontSizeH4"] = 20,
        ["FontSizeH5"] = 18,
        ["FontSizeH6"] = 16,};

    /// <summary>
    /// 系统字体缓存（延迟加载）
    /// </summary>
    private List<string>? _systemFontsCache;

    /// <summary>
    /// 字体缓存锁
    /// </summary>
    private readonly object _fontCacheLock = new();

    /// <summary>
    /// FontFamily对象缓存（避免重复创建）
    /// </summary>
    private readonly Dictionary<string, WeakReference<FontFamily>> _fontFamilyCache = new();

    /// <summary>
    /// 当前使用的FontFamily（强引用，确保不被GC回收）
    /// </summary>
    private FontFamily? _currentFontFamily;

    private FontService() { }

    /// <summary>
    /// 初始化字体服务，从配置中加载字体缩放设置
    /// </summary>
    public static void Initialize()
    {
        var scale = ConfigurationManager.Current.GetValue(ConfigurationKeys.FontScale, DefaultScale);
        Instance.ApplyFontScale(scale, false);
    }

    /// <summary>
    /// 应用字体缩放
    /// </summary>
    /// <param name="scale">缩放比例 (0.8 - 1.5)</param>
    /// <param name="saveToConfig">是否保存到配置</param>
    public void ApplyFontScale(double scale, bool saveToConfig = true)
    {
        // 限制缩放范围
        scale = Math.Clamp(scale, MinScale, MaxScale);
        
        // 如果缩放值没有变化，直接返回
        if (Math.Abs(CurrentScale - scale) < 0.001)
            return;
        CurrentScale = scale;

        // 重用现有的ScaleTransform对象，避免频繁创建
        if (ScaleTransform.ScaleX != scale || ScaleTransform.ScaleY != scale)
        {
            ScaleTransform.ScaleX = scale;
            ScaleTransform.ScaleY = scale;
        }

        var app = Application.Current;
        if (app?.Resources == null) return;

        // 更新各级字体大小资源
        foreach (var (key, baseSize) in BaseFontSizes)
        {
            var scaledSize = Math.Round(baseSize * scale, 1);
            app.Resources[key] = scaledSize;
        }

        // 保存到配置
        if (saveToConfig)
        {
            ConfigurationManager.Current.SetValue(ConfigurationKeys.FontScale, scale);
        }
    }

    /// <summary>
    /// 重置字体缩放为默认值
    /// </summary>
    public void ResetFontScale()
    {
        ApplyFontScale(DefaultScale);
    }

    /// <summary>
    /// 获取系统已安装的字体列表（延迟加载,使用缓存）
    /// 注意: 此方法会加载所有系统字体元数据,包含大量字符串(版权信息等),
    /// 因此使用延迟加载和缓存策略来减少内存占用
    /// </summary>
    /// <returns>字体名称列表</returns>
    public IEnumerable<string> GetSystemFonts()
    {
        // 使用缓存避免重复加载字体元数据
        if (_systemFontsCache != null)
        {
            return _systemFontsCache;
        }

        lock (_fontCacheLock)
        {
            // 双重检查锁定
            if (_systemFontsCache != null)
            {
                return _systemFontsCache;
            }

            try
            {
                // 只提取字体名称，不使用string.Intern以避免永久占用内存
                // string.Intern会将字符串放入字符串池，永远不会被GC回收
                _systemFontsCache = FontManager.Current.SystemFonts
                    .Select(f => f.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                LoggerHelper.Info($"[字体服务]已缓存 {_systemFontsCache.Count} 个系统字体");
                return _systemFontsCache;
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"获取系统字体列表失败: {ex.Message}");
                _systemFontsCache = new List<string>();
                return _systemFontsCache;
            }
        }
    }

    /// <summary>
    /// 清除字体缓存（用于释放内存）
    /// </summary>
    public void ClearFontCache()
    {
        lock (_fontCacheLock)
        {
            // 清除系统字体缓存
            _systemFontsCache?.Clear();
            _systemFontsCache = null;
            // 清除FontFamily缓存（保留当前使用的）
            var keysToRemove = new List<string>();
            foreach (var kvp in _fontFamilyCache)
            {
                if (!kvp.Value.TryGetTarget(out var fontFamily) || fontFamily != _currentFontFamily)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _fontFamilyCache.Remove(key);
            }
            LoggerHelper.Info($"[字体服务]已清除字体缓存，移除了 {keysToRemove.Count} 个未使用的FontFamily对象");
        }
    }

    /// <summary>
    /// 获取或创建FontFamily对象（使用缓存避免重复创建）
    /// </summary>
    /// <param name="fontName">字体名称</param>
    /// <returns>FontFamily对象，如果创建失败返回null</returns>
    private FontFamily? GetOrCreateFontFamily(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
            return null;

        lock (_fontCacheLock)
        {
            //尝试从缓存获取
            if (_fontFamilyCache.TryGetValue(fontName, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var cachedFont))
                {
                    return cachedFont;
                }else
                {
                    //弱引用已失效，移除
                    _fontFamilyCache.Remove(fontName);
                }
            }

            // 创建新的FontFamily对象
            try
            {
                var fontFamily = new FontFamily(fontName);
                _fontFamilyCache[fontName] = new WeakReference<FontFamily>(fontFamily);
                return fontFamily;
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"创建FontFamily失败 ({fontName}): {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 应用字体
    /// </summary>
    /// <param name="fontName">字体名称</param>
    public void ApplyFontFamily(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName)) return;

        var app = Application.Current;
        if (app?.Resources == null) return;

        try
        {
            // 使用缓存机制获取FontFamily对象
            var fontFamily = GetOrCreateFontFamily(fontName);
            if (fontFamily == null)
            {
                LoggerHelper.Warning($"无法应用字体: {fontName}");
                return;
            }

            // 保存当前FontFamily的强引用，防止被GC回收
            _currentFontFamily = fontFamily;
            
            app.Resources["DefaultFontFamily"] = fontFamily;
            ConfigurationManager.Current.SetValue(ConfigurationKeys.FontFamily, fontName);
            
            LoggerHelper.Info($"[字体服务]已应用字体: {fontName}");
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"应用字体失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取缩放后的字体大小
    /// </summary>
    /// <param name="baseSize">基础字体大小</param>
    /// <returns>缩放后的字体大小</returns>
    public double GetScaledFontSize(double baseSize)
    {
        return Math.Round(baseSize * CurrentScale, 1);
    }

    /// <summary>
    /// 强制清理所有字体资源（用于应用退出或内存紧急情况）
    /// </summary>
    public void ForceCleanupAllFontResources()
    {
        lock (_fontCacheLock)
        {
            _systemFontsCache?.Clear();
            _systemFontsCache = null;
            _fontFamilyCache.Clear();
            _currentFontFamily = null;
            
            LoggerHelper.Info("[字体服务]已强制清理所有字体资源");
        }
    }
}