using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MFAAvalonia.Extensions;
using MFAAvalonia.ViewModels.UsersControls.Settings;
using System;

namespace MFAAvalonia.Views.UserControls;

public partial class AddTaskDialogView : UserControl
{
    public AddTaskDialogView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) =>
        {
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        };
    }

    private void SearchBar_OnSearchStarted(object sender, RoutedEventArgs e)
    {
        if (DataContext is AddTaskDialogViewModel vm)
            vm.ApplyFilter(SearchBar.Text ?? string.Empty);
    }

    private void TaskItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: AddTaskItemViewModel item })
        {
            item.IncrementCommand.Execute(null);
        }
    }

    private void MinusItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (sender is Border { DataContext: AddTaskItemViewModel item })
        {
            item.DecrementCommand.Execute(null);
        }
    }

    private void BadgeItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (sender is Border { DataContext: AddTaskItemViewModel item })
        {
            item.ResetCountCommand.Execute(null);
        }
    }
}
