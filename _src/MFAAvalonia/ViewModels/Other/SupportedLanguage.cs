using CommunityToolkit.Mvvm.ComponentModel;

namespace MFAAvalonia.ViewModels.Other;

public partial class SupportedLanguage(string key, string name) : ViewModelBase
{
    [ObservableProperty] private string _name = name;
    [ObservableProperty] private string _key = key;
}
