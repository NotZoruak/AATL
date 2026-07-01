using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class TimerSettingsUserControl : UserControl
{
    public TimerSettingsUserControl()
    {
        DataContext = Instances.TimerSettingsUserControlModel;
        InitializeComponent();
    }
}

