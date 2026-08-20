using Avalonia.Controls;
using MFAAvalonia.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MFAAvalonia.Views.Pages;

/// <summary>本丸页面容器：提供三个固定顺序的下级页面。</summary>
public partial class HonmaruView : UserControl
{
    public HonmaruView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<HonmaruViewModel>();
    }
}
