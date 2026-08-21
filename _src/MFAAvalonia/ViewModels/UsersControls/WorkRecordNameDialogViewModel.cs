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

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Name);

    public WorkRecordNameDialogViewModel(ISukiDialog dialog, Action<string> onConfirm)
    {
        Dialog = dialog;
        _onConfirm = onConfirm;
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
