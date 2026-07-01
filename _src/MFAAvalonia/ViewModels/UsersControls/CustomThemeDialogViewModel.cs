using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using MFAAvalonia.ViewModels;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Models;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Views.UserControls;

public partial class CustomThemeDialogViewModel : ViewModelBase
{
    private readonly SukiTheme _theme;
    private readonly ISukiDialog _dialog;

    private sealed record ThemePreset(string Name, Color PrimaryColor, Color AccentColor);

    private static readonly IReadOnlyList<ThemePreset> s_themePresets =
    [
        new("Pink", Colors.DeepPink, Colors.Pink),
        new("Purple", Color.Parse("#7C3AED"), Color.Parse("#A78BFA")),
        new("Ocean", Color.Parse("#0284C7"), Color.Parse("#38BDF8")),
        new("Mint", Color.Parse("#059669"), Color.Parse("#6EE7B7")),
        new("Sunset", Color.Parse("#EA580C"), Color.Parse("#FB7185")),
        new("Amber", Color.Parse("#D97706"), Color.Parse("#FBBF24")),
        new("Coral", Color.Parse("#F97316"), Color.Parse("#FB7185")),
        new("Lavender", Color.Parse("#8B5CF6"), Color.Parse("#C4B5FD")),
        new("Slate", Color.Parse("#475569"), Color.Parse("#94A3B8")),
        new("Forest", Color.Parse("#166534"), Color.Parse("#4ADE80"))
    ];

    private static int s_nextPresetIndex;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private Color _primaryColor;
    [ObservableProperty] private Color _accentColor;

    public CustomThemeDialogViewModel(SukiTheme theme, ISukiDialog dialog)
    {
        _theme = theme;
        _dialog = dialog;
        var preset = SelectNextPreset(theme);
        DisplayName = preset.Name;
        PrimaryColor = preset.PrimaryColor;
        AccentColor = preset.AccentColor;
    }

    private static ThemePreset SelectNextPreset(SukiTheme theme)
    {
        var existingThemeNames = theme.ColorThemes
            .Select(colorTheme => colorTheme.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet();

        for (var offset = 0; offset < s_themePresets.Count; offset++)
        {
            var presetIndex = (s_nextPresetIndex + offset) % s_themePresets.Count;
            var preset = s_themePresets[presetIndex];
            if (existingThemeNames.Contains(preset.Name))
            {
                continue;
            }

            s_nextPresetIndex = (presetIndex + 1) % s_themePresets.Count;
            return preset;
        }

        var fallbackPreset = s_themePresets[s_nextPresetIndex % s_themePresets.Count];
        var suffix = 2;
        var fallbackName = fallbackPreset.Name;
        while (existingThemeNames.Contains(fallbackName))
        {
            fallbackName = $"{fallbackPreset.Name} {suffix}";
            suffix++;
        }

        s_nextPresetIndex = (s_nextPresetIndex + 1) % s_themePresets.Count;
        return fallbackPreset with { Name = fallbackName };
    }

    [RelayCommand]
    private void TryCreateTheme()
    {
        if (string.IsNullOrEmpty(DisplayName)) return;
        if (_theme.ColorThemes.Any(t => t.DisplayName == DisplayName))
        {
            ToastHelper.Error(LangKeys.ColorThemeAlreadyExists.ToLocalization());
            _dialog.Dismiss();
            return;
        }
        var color = new SukiColorTheme(DisplayName, PrimaryColor, AccentColor);
        Instances.GuiSettingsUserControlModel.AddOtherColor(color);
        _theme.AddColorTheme(color);
        _theme.ChangeColorTheme(color);
        _dialog.Dismiss();
    }
}
