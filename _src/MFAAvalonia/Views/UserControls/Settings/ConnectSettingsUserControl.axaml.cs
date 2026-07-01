using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class ConnectSettingsUserControl : UserControl
{
    public ConnectSettingsUserControl()
    {
        DataContext = Instances.ConnectSettingsUserControlModel;
        InitializeComponent();
    }
}

