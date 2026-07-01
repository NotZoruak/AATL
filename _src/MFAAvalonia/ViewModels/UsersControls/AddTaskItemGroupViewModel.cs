using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MFAAvalonia.ViewModels.UsersControls.Settings;

public partial class AddTaskItemGroupViewModel : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    [ObservableProperty] private string _label = string.Empty;

    [ObservableProperty] private string _description = string.Empty;

    [ObservableProperty] private bool _hasDescription;

    [ObservableProperty] private string _icon = string.Empty;

    [ObservableProperty] private bool _hasIcon;

    [ObservableProperty] private bool _isExpanded = true;

    public ObservableCollection<AddTaskItemViewModel> Items { get; } = [];

    public void ResetItems(IEnumerable<AddTaskItemViewModel> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
    }
}
