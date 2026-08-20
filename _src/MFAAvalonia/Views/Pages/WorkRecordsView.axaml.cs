using Avalonia.Controls;
using MFAAvalonia.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace MFAAvalonia.Views.Pages;

/// <summary>工作记录页：左侧运行记录列表 + 右侧统计卡片</summary>
public partial class WorkRecordsView : UserControl
{
    public WorkRecordsView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<WorkRecordsViewModel>();
    }
}
