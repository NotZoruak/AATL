using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using MFAAvalonia.Extensions;
using System;

namespace MFAAvalonia.Helper.ValueType;

public partial class MFAHotKey : ObservableObject
{
    public static readonly MFAHotKey NOTSET = new(true)
    {
        ResourceKey = "HotKeyNotSet",
    };

    public static readonly MFAHotKey ERROR = new(true)
    {
        ResourceKey = "HotKeyOccupiedWarning",
    };

    public static readonly MFAHotKey PRESSING = new(true)
    {
        ResourceKey = "HotKeyPressing",
    };

    [ObservableProperty] private bool _isTip;
    [ObservableProperty] private KeyGesture? _gesture;
    [ObservableProperty] private string _resourceKey;
    [ObservableProperty] private string[] _resourceArgsKeys = Array.Empty<string>();
    partial void OnResourceKeyChanged(string value)
    {
        UpdateName();

    }
    
    partial void OnResourceArgsKeysChanged(string[] value)
    {
        UpdateName();
    }

    public MFAHotKey(bool isTip = false)
    {
        IsTip = isTip;
        LanguageHelper.LanguageChanged += (_, _) => UpdateName();
        UpdateName();
    }

    public MFAHotKey(KeyGesture gesture)
    {
        Gesture = gesture;
        UpdateName();
    }

    [ObservableProperty] private string _name = string.Empty;

    public void UpdateName()
    {
        Name = IsTip
            ? (ResourceArgsKeys.Length > 0
                ? ResourceKey.ToLocalizationFormatted(true, ResourceArgsKeys)
                : ResourceKey.ToLocalization())
            : (Gesture?.ToString() ?? "");
    }

    // 保留 Equals/GetHashCode 等核心逻辑（修改为比较 KeyGesture）
    public bool Equals(MFAHotKey? other) =>
        other != null && Gesture == other.Gesture;

    // 新增 KeyGesture 解析逻辑

    public static MFAHotKey Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return NOTSET;
        try
        {
            return new MFAHotKey(KeyGesture.Parse(input));
        }
        catch (Exception)
        {
            return NOTSET;
        }
    }

    public override string ToString()
        => Name;
}
