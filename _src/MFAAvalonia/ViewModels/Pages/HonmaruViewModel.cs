using CommunityToolkit.Mvvm.ComponentModel;

namespace MFAAvalonia.ViewModels.Pages;

/// <summary>本丸页面容器：承载仓库、工作记录和刀帐三个固定页签。</summary>
public partial class HonmaruViewModel : ViewModelBase
{
    /// <summary>当前选中的页签索引，默认打开仓库。</summary>
    [ObservableProperty]
    private int _selectedTabIndex;
}
