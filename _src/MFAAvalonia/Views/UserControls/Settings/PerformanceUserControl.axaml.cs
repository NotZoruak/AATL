using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class PerformanceUserControl : UserControl
{
    public PerformanceUserControl()
    {
        DataContext = Instances.PerformanceUserControlModel;
        InitializeComponent();
    }
}

