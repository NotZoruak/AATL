using CommunityToolkit.Mvvm.ComponentModel;
using MFAAvalonia.Configuration;
using MFAAvalonia.Helper;
using MFAAvalonia.Helper.ValueType;
using MFAAvalonia.Views.Windows;
using System;
using System.IO;

namespace MFAAvalonia.ViewModels.Windows;

public partial class ChangelogViewModel : ViewModelBase
{
    public static readonly string ChangelogFileName = "Changelog.md";
    public static readonly string ReleaseFileName = "Release.md";
    [ObservableProperty] private string _announcementInfo = string.Empty;

    [ObservableProperty] private bool _doNotRemindThisChangelogAgain = Convert.ToBoolean(GlobalConfiguration.GetValue(ConfigurationKeys.DoNotShowChangelogAgain, bool.FalseString));
    partial void OnDoNotRemindThisChangelogAgainChanged(bool value)
    {
        GlobalConfiguration.SetValue(ConfigurationKeys.DoNotShowChangelogAgain, value.ToString());
    }


    [ObservableProperty] private AnnouncementType _type = AnnouncementType.Changelog;

    public static bool CheckReleaseNote()
    {
        var result = false;
        var viewModel = new ChangelogViewModel
        {
            Type = AnnouncementType.Release,
        };
        try
        {
            var resourcePath = AppPaths.ResourceDirectory;
            var mdPath = Path.Combine(resourcePath, ReleaseFileName);

            
            if (File.Exists(mdPath))
            {
                var content = File.ReadAllText(mdPath);
                viewModel.AnnouncementInfo = content;
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"读取Release Note文件失败: {ex.Message}");
            viewModel.AnnouncementInfo = "";
        }
        finally
        {

            if (!string.IsNullOrWhiteSpace(viewModel.AnnouncementInfo) && !viewModel.AnnouncementInfo.Trim().Equals("placeholder", StringComparison.OrdinalIgnoreCase))
            {
                var announcementView = new ChangelogView
                {
                    DataContext = viewModel
                };
                announcementView.Show();
                result = true;
            }

        }
        return result;
    }

    public static bool CheckChangelog()
    {
        var result = false;
        var viewModel = new ChangelogViewModel
        {
            Type = AnnouncementType.Changelog,
        };
        if (viewModel.DoNotRemindThisChangelogAgain) return false;
        try
        {
            var resourcePath = AppPaths.ResourceDirectory;
            var mdPath = Path.Combine(resourcePath, ChangelogFileName);

            if (File.Exists(mdPath))
            {
                var content = File.ReadAllText(mdPath);
                viewModel.AnnouncementInfo = content;
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"读取公告文件失败: {ex.Message}");
            viewModel.AnnouncementInfo = "";
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(viewModel.AnnouncementInfo) && !viewModel.AnnouncementInfo.Trim().Equals("placeholder", StringComparison.OrdinalIgnoreCase))
            {
                var announcementView = new ChangelogView
                {
                    DataContext = viewModel
                };
                announcementView.Show();
                result = true;
            }
        }
        return result;
    }
}
