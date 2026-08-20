using Avalonia.Controls;
using MFAAvalonia.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MFAAvalonia.Views.Pages;

/// <summary>仓库页面。</summary>
public partial class WarehouseView : UserControl
{
    public WarehouseView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<WarehouseViewModel>();
    }
}
