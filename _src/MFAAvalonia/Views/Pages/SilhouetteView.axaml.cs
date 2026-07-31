using Avalonia.Controls;
using MFAAvalonia.Helper;
using MFAAvalonia.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MFAAvalonia.Views.Pages;

public partial class SilhouetteView : UserControl
{
    public SilhouetteView()
    {
        try
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<SilhouetteViewModel>();
        }
        catch (System.Exception ex)
        {
            LoggerHelper.Error($"[SilhouetteView] 初始化失败: {ex}", ex);
        }
    }
}
