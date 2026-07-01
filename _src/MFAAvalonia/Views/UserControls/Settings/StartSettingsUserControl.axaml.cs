using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class StartSettingsUserControl : UserControl
{
    public StartSettingsUserControl()
    {
        DataContext = Instances.StartSettingsUserControlModel;
        InitializeComponent();
    }
}

