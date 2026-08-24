using Avalonia.Controls;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class AllowListUserControl : UserControl
{
    public AllowListUserControl()
    {
        DataContext = Instances.AllowListUserControlModel;
        InitializeComponent();
    }
}
