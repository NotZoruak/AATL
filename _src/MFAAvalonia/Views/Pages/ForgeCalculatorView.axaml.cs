using Avalonia.Controls;
using MFAAvalonia.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MFAAvalonia.Views.Pages;

public partial class ForgeCalculatorView : UserControl
{
    public ForgeCalculatorView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ForgeCalculatorViewModel>();
    }
}
