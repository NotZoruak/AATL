using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class ExternalNotificationSettingsUserControl : UserControl
{
    public ExternalNotificationSettingsUserControl()
    {
        DataContext = Instances.ExternalNotificationSettingsUserControlModel;
        InitializeComponent();
    }
}

 