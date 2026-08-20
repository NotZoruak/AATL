using Avalonia.Controls;
using MFAAvalonia.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MFAAvalonia.Views.Pages;

/// <summary>刀帐页面。</summary>
public partial class SwordBookView : UserControl
{
    public SwordBookView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SwordBookViewModel>();
    }
}
