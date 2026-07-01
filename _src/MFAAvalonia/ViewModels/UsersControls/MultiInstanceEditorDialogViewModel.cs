using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using MFAAvalonia.ViewModels.Other;
using SukiUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MFAAvalonia.ViewModels.UsersControls;

public partial class MultiInstanceEditorDialogViewModel : ViewModelBase
{
    [ObservableProperty] private double _index = 0;
    [ObservableProperty] private string _emulator = "mumu";
    public ISukiDialog Dialog { get; set; }
    public MultiInstanceEditorDialogViewModel(ISukiDialog dialog)
    {
        Dialog = dialog;
    }
    public ObservableCollection<LocalizationViewModel> EmulatorList =>
    [
        new(LangKeys.MuMuEmulator12)
        {
            Other = "mumu"
        },
        new(LangKeys.LDPlayer)
        {
            Other = "ldplayer"
        },
        new(LangKeys.Nox)
        {
            Other = "nox"
        },
        new(LangKeys.BlueStacks)
        {
            Other = "bluestacks"
        },
        new(LangKeys.XYAZ)
        {
            Other = "xyaz"
        },
    ];

    public static readonly Dictionary<string, string> EmulatorMultiOpenArgumentPrefixes = new()
    {
        {
            "mumu", "-v "
        },
        {
            "ldplayer", "index="
        },
        {
            "xyaz", "--index "
        },
        {
            "nox", "-index "
        },
        {
            "bluestacks", "--index "
        }
    };

    [RelayCommand]
    public void Save()
    {
        if (EmulatorMultiOpenArgumentPrefixes.TryGetValue(Emulator, out var emulatorPrefix ))
        {
            Instances.StartSettingsUserControlModel.EmulatorConfig = $"{emulatorPrefix}{Convert.ToInt32(Index)}";
        }
        Dialog.Dismiss();
    }
   
    /// <summary>
    /// 静态方法：自动匹配所有模拟器前缀规则，从 EmulatorConfig 字符串中反向提取 Index
    /// 格式不匹配、提取失败或无有效数字时返回 -1
    /// </summary>
    /// <param name="emulatorConfig">要解析的 EmulatorConfig 字符串（如 "-v 5"、"index=3"）</param>
    /// <returns>提取到的 Index，失败返回 -1</returns>
    public static int TryExtractIndexFromEmulatorConfig(string emulatorConfig)
    {
        emulatorConfig = emulatorConfig.Trim();
        if (string.IsNullOrWhiteSpace(emulatorConfig))
        {
            return -1;
        }

        foreach (var (_, targetPrefix) in EmulatorMultiOpenArgumentPrefixes)
        {
            var prefixIndex = emulatorConfig.IndexOf(targetPrefix, StringComparison.Ordinal);
            if (prefixIndex < 0)
            {
                continue;
            }

            var indexStart = prefixIndex + targetPrefix.Length;
            while (indexStart < emulatorConfig.Length && char.IsWhiteSpace(emulatorConfig[indexStart]))
            {
                indexStart++;
            }

            if (indexStart >= emulatorConfig.Length || !char.IsDigit(emulatorConfig[indexStart]))
            {
                continue;
            }

            var indexEnd = indexStart;
            while (indexEnd < emulatorConfig.Length && char.IsDigit(emulatorConfig[indexEnd]))
            {
                indexEnd++;
            }

            var indexPart = emulatorConfig[indexStart..indexEnd];
            if (int.TryParse(indexPart, out int index) && index >= 0)
            {
                return index;
            }
        }

        return -1;
    }
}
