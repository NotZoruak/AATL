using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MFAAvalonia.Helper;
using System.Linq;

namespace MFAAvalonia.Views.UserControls.Settings;

public partial class GameSettingsUserControl : UserControl
{
    public GameSettingsUserControl()
    {
        DataContext = Instances.GameSettingsUserControlModel;
        InitializeComponent();
    }
    
}

