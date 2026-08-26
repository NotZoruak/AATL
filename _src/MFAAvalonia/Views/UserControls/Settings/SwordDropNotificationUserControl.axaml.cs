using Avalonia.Controls;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class SwordDropNotificationUserControl : UserControl
{
    public SwordDropNotificationUserControl()
    {
        DataContext = Instances.SwordDropNotificationUserControlModel;
        InitializeComponent();
    }
}
