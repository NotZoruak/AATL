using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAAvalonia.Helper;

namespace MFAAvalonia.Views.Pages;

public partial class ResourcesView : UserControl
{
    public ResourcesView()
    {
        DataContext = Instances.ResourcesViewModel;
        InitializeComponent();
    }
}

