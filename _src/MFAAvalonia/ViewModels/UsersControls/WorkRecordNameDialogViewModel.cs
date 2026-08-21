using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.ViewModels;
using SukiUI.Dialogs;
using System;

namespace MFAAvalonia.ViewModels.UsersControls;

public partial class WorkRecordNameDialogViewModel : ViewModelBase
{
    public ISukiDialog Dialog { get; }
    private readonly Action<string> _onConfirm;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _name = "";

    [ObservableProperty]
    private string _prompt = "请输入保存记录名称";

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Name);

    public WorkRecordNameDialogViewModel(
        ISukiDialog dialog,
        Action<string> onConfirm,
        string initialName = "",
        string prompt = "请输入保存记录名称")
    {
        Dialog = dialog;
        _onConfirm = onConfirm;
        Name = initialName;
        Prompt = prompt;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        _onConfirm(Name.Trim());
        Dialog.Dismiss();
    }

    [RelayCommand]
    private void Cancel() => Dialog.Dismiss();
}
