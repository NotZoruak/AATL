using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Platform.Storage;
using MaaFramework.Binding.Interop.Native;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper;
using MFAAvalonia.Helper.Converters;
using MFAAvalonia.ViewModels.Other;
using MFAAvalonia.ViewModels.Windows;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.ViewModels.UsersControls.Settings;

public partial class VersionUpdateSettingsUserControlModel : ViewModelBase
{
    [ObservableProperty] private bool _showLocalPackageUpdate;

    public enum UpdateProxyType
    {
        Http,
        Socks5
    }

    protected override void Initialize()
    {
        try
        {
            MaaFwVersion = MaaUtility.MaaVersion();
        }
        catch (Exception e)
        {
            MaaFwVersion = "v5.0.0";
            LoggerHelper.Error($"读取 MaaFramework 版本失败，已回退默认值：原因={e.Message}", e);
        }
        LanguageHelper.LanguageChanged += (_, _) => UpdateCdkExpireDisplay();
        ConfigurationManager.ConfigurationSwitched += OnConfigurationSwitched;
        RefreshDebugActionsVisibility();
        StopCountdownTimer();
        base.Initialize();
    }

    private void OnConfigurationSwitched(string _)
    {
        RefreshDebugActionsVisibility();
    }

    public void RefreshDebugActionsVisibility()
    {
        if (ConfigurationManager.Current.TryGetValue<bool>("Debug", out var debugValue))
        {
            ShowLocalPackageUpdate = debugValue;
            LoggerHelper.Info($"本地更新包按钮可见性已刷新：来源=config.Debug(bool)，值={debugValue}");
            return;
        }

        var rawValue = ConfigurationManager.Current.GetValue("Debug", string.Empty);
        ShowLocalPackageUpdate = bool.TryParse(rawValue, out var parsed) && parsed;
        LoggerHelper.Info($"本地更新包按钮可见性已刷新：来源=config.Debug(string)，原始值={rawValue}，解析结果={ShowLocalPackageUpdate}");
    }

    [ObservableProperty] private string _maaFwVersion = "";
    [ObservableProperty] private string _mfaVersion = RootViewModel.Version;
    [ObservableProperty] private string _resourceVersion = string.Empty;
    [ObservableProperty] private bool _showResourceVersion;
    [ObservableProperty] private long _cdkExpiredTime = 0;
    [ObservableProperty] private bool _cdkTextVisible = false;
    [ObservableProperty] private string _cdkExpireText = string.Empty;
    [ObservableProperty] private IBrush _cdkExpireColor = Brushes.MediumSeaGreen;
    private Timer? _countdownTimer;

    partial void OnCdkExpiredTimeChanged(long value)
    {
        UpdateCdkExpireDisplay();
    }

    private void UpdateCdkExpireDisplay()
    {
        if (CdkExpiredTime <= 0)
        {
            CdkExpireText = string.Empty;
            CdkTextVisible = false;
            StopCountdownTimer();
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remaining = CdkExpiredTime - now;

        if (remaining <= 0)
        {
            CdkExpireText = LangKeys.MirrorCdkExpired.ToLocalization();
            CdkExpireColor = Brushes.Red;
            CdkTextVisible = true;
            StopCountdownTimer();
            return;
        }

        // 小于1天显示黄色
        if (remaining < 86400)
        {
            CdkExpireColor = Brushes.Orange;
            StartCountdownTimer();
        }
        else
        {
            CdkExpireColor = Brushes.MediumSeaGreen;
        }

        // 根据时间长短显示不同单位
        if (remaining >= 86400) // >= 1天
        {
            var days = remaining / 86400;
            CdkExpireText = string.Format(LangKeys.CdkExpireInDays.ToLocalization(), days);
        }
        else if (remaining >= 3600) // >= 1小时
        {
            var hours = remaining / 3600;
            CdkExpireText = string.Format(LangKeys.CdkExpireInHours.ToLocalization(), hours);
        }
        else if (remaining >= 120) // >= 2分钟
        {
            var minutes = remaining / 60;
            CdkExpireText = string.Format(LangKeys.CdkExpireInMinutes.ToLocalization(), minutes);
        }
        else // < 2分钟
        {
            CdkExpireText = string.Format(LangKeys.CdkExpireInSeconds.ToLocalization(), remaining);
        }
        CdkTextVisible = true;
    }
    
    private void StartCountdownTimer()
    {
        if (_countdownTimer != null)
            return; // 定时器已经在运行

        _countdownTimer = new Timer(_ =>
        {
            DispatcherHelper.RunOnMainThread(() =>
            {
                if (CdkExpiredTime <= 0)
                {
                    StopCountdownTimer();
                    return;
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var remaining = CdkExpiredTime - now;

                if (remaining <= 0)
                {
                    CdkExpireText = LangKeys.MirrorCdkExpired.ToLocalization();
                    CdkExpireColor = Brushes.Red;
                    CdkTextVisible = true;
                    StopCountdownTimer();
                }
                else if (remaining < 86400) // 仍然小于2分钟，继续倒计时
                {
                    CdkExpireText = string.Format(LangKeys.CdkExpireInSeconds.ToLocalization(), remaining);
                }
                else // 超过2分钟了，停止倒计时并更新显示
                {
                    UpdateCdkExpireDisplay();
                }
            });
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void StopCountdownTimer()
    {
        if (_countdownTimer != null)
        {
            _countdownTimer.Dispose();
            _countdownTimer = null;
        }
    }

    partial void OnResourceVersionChanged(string value)
    {
        ShowResourceVersion = !string.IsNullOrWhiteSpace(value);
    }

    public ObservableCollection<LocalizationViewModel> DownloadSourceList =>
    [
        new()
        {
            Name = "GitHub"
        },
        new(LangKeys.MirrorChyan),
    ];

    [ObservableProperty] private int _downloadSourceIndex = ConfigurationManager.Current.GetValue(ConfigurationKeys.DownloadSourceIndex, 1);

    partial void OnDownloadSourceIndexChanged(int value)
    {
        ConfigurationManager.Current.SetValue(ConfigurationKeys.DownloadSourceIndex, value);
    }

    public ObservableCollection<LocalizationViewModel> UIUpdateChannelList =>
    [
        new(LangKeys.AlphaVersion),
        new(LangKeys.BetaVersion),
        new(LangKeys.StableVersion),
    ];

    [ObservableProperty] private int _uIUpdateChannelIndex = ConfigurationManager.Current.GetValue(ConfigurationKeys.UIUpdateChannelIndex, 2);

    partial void OnUIUpdateChannelIndexChanged(int value)
    {
        ConfigurationManager.Current.SetValue(ConfigurationKeys.UIUpdateChannelIndex, value);
    }

    public ObservableCollection<LocalizationViewModel> ResourceUpdateChannelList =>
    [
        new(LangKeys.AlphaVersion),
        new(LangKeys.BetaVersion),
        new(LangKeys.StableVersion),
    ];

    [ObservableProperty] private int _resourceUpdateChannelIndex = ConfigurationManager.Current.GetValue(ConfigurationKeys.ResourceUpdateChannelIndex, 2);

    partial void OnResourceUpdateChannelIndexChanged(int value) => HandlePropertyChanged(ConfigurationKeys.ResourceUpdateChannelIndex, value);

    [ObservableProperty] private string _gitHubToken = SimpleEncryptionHelper.Decrypt(ConfigurationManager.Current.GetValue(ConfigurationKeys.GitHubToken, string.Empty));

    partial void OnGitHubTokenChanged(string value) => HandlePropertyChanged(ConfigurationKeys.GitHubToken, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _cdkPassword = SimpleEncryptionHelper.Decrypt(ConfigurationManager.Current.GetValue(ConfigurationKeys.DownloadCDK, string.Empty));

    partial void OnCdkPasswordChanged(string value) => HandlePropertyChanged(ConfigurationKeys.DownloadCDK, SimpleEncryptionHelper.Encrypt(value),(() =>
    {
        CdkExpiredTime = 0;
        CdkTextVisible = false;
    }));

    [ObservableProperty] private bool _enableCheckVersion = ConfigurationManager.Current.GetValue(ConfigurationKeys.EnableCheckVersion, true);

    [ObservableProperty] private bool _enableAutoUpdateResource = ConfigurationManager.Current.GetValue(ConfigurationKeys.EnableAutoUpdateResource, false);

    [ObservableProperty] private bool _enableAutoUpdateMFA = ConfigurationManager.Current.GetValue(ConfigurationKeys.EnableAutoUpdateMFA, false);

    partial void OnEnableCheckVersionChanged(bool value)
    {
        ConfigurationManager.Current.SetValue(ConfigurationKeys.EnableCheckVersion, value);
    }

    partial void OnEnableAutoUpdateResourceChanged(bool value)
    {
        ConfigurationManager.Current.SetValue(ConfigurationKeys.EnableAutoUpdateResource, value);
    }

    partial void OnEnableAutoUpdateMFAChanged(bool value)
    {
        ConfigurationManager.Current.SetValue(ConfigurationKeys.EnableAutoUpdateMFA, value);
    }
    [ObservableProperty] private string _proxyAddress = ConfigurationManager.Current.GetValue(ConfigurationKeys.ProxyAddress, string.Empty);
    [ObservableProperty] private UpdateProxyType _proxyType = ConfigurationManager.Current.GetValue(ConfigurationKeys.ProxyType, UpdateProxyType.Http, UpdateProxyType.Http, new UniversalEnumConverter<UpdateProxyType>());
    public ObservableCollection<LocalizationViewModel> ProxyTypeList =>
    [
        new("HTTP Proxy")
        {
            Other = UpdateProxyType.Http
        },
        new("SOCKS5 Proxy")
        {
            Other = UpdateProxyType.Socks5
        },
    ];

    partial void OnProxyAddressChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ProxyAddress, value);

    partial void OnProxyTypeChanged(UpdateProxyType value) => HandlePropertyChanged(ConfigurationKeys.ProxyType, value.ToString());

    [RelayCommand]
    private void UpdateResource()
    {
        VersionChecker.UpdateResourceAsync();
    }

    [RelayCommand]
    private void RedownloadResource()
    {
        VersionChecker.UpdateResourceAsync("v0.0.0");
    }

    [RelayCommand]
    private async Task UpdateResourceFromLocalPackage()
    {
        var storageProvider = Instances.StorageProvider;
        if (storageProvider == null)
        {
            ToastHelper.Warn(LangKeys.Warning.ToLocalization(), LangKeys.PlatformNotSupportedOperation.ToLocalization());
            return;
        }

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择本地更新包",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Archive")
                {
                    Patterns = ["*.zip", "*.7z", "*.tar", "*.gz", "*.tgz", "*.tar.gz"]
                }
            ]
        });

        if (result is { Count: > 0 } && result[0].TryGetLocalPath() is { } path)
        {
            VersionChecker.UpdateResourceFromLocalPackageAsync(path);
        }
    }

    [RelayCommand]
    private void CheckResourceUpdate()
    {
        VersionChecker.CheckResourceVersionAsync();
    }

    [RelayCommand]
    private void UpdateMFA()
    {
        VersionChecker.UpdateMFAAsync();
    }
    [RelayCommand]
    private void CheckMFAUpdate()
    {
        VersionChecker.CheckMFAVersionAsync();
    }
    [RelayCommand]
    private void UpdateMaaFW()
    {
        VersionChecker.UpdateMaaFwAsync();
    }
    
    [RelayCommand]
    private void QueryCdkTime()
    {
        CdkExpiredTime = 0;
        CdkTextVisible = false;
        VersionChecker.CheckCDKAsync();
    }
}
