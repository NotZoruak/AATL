using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class HotKeySettingsUserControl : UserControl
{
    public HotKeySettingsUserControl()
    {
        DataContext = Instances.SettingsViewModel;
        InitializeComponent();
    }
}

