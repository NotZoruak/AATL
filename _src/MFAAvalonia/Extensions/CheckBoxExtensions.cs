using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit.Utils;
using System;

namespace MFAAvalonia.Extensions;

public class CheckBoxExtensions
{
    public static readonly AttachedProperty<bool> EnableRightClickNullProperty =
        AvaloniaProperty.RegisterAttached<CheckBox, bool>(
            "EnableRightClickNull", typeof(CheckBoxExtensions), false);

    public static bool GetEnableRightClickNull(CheckBox element) => element.GetValue(EnableRightClickNullProperty);

    public static void SetEnableRightClickNull(CheckBox element, bool value) => element.SetValue(EnableRightClickNullProperty, value);
    static CheckBoxExtensions()
    {
        EnableRightClickNullProperty.Changed.Subscribe(OnEnableRightClickNullChanged);
        LimitDragDropProperty.Changed.Subscribe(OnLimitDragDropChanged);
    }

    private static void OnEnableRightClickNullChanged(AvaloniaPropertyChangedEventArgs<bool> args)
    {
        if (args.Sender is CheckBox checkBox)
        {
            if (args.NewValue.Value)
            {
                checkBox.AddHandler(
                    InputElement.PointerPressedEvent,
                    (EventHandler<PointerPressedEventArgs>)HandlePointerPressed,
                    RoutingStrategies.Tunnel);
            }
            else
            {
                checkBox.RemoveHandler(InputElement.PointerPressedEvent, (EventHandler<PointerPressedEventArgs>)HandlePointerPressed);
            }
        }
    }
    private static void HandlePointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            if (e.GetCurrentPoint(TopLevel.GetTopLevel(checkBox)).Properties.IsRightButtonPressed)
            {
                checkBox.IsChecked = checkBox.IsChecked == null ? false : null;
                e.Handled = true;
            }
            else  if (e.GetCurrentPoint(TopLevel.GetTopLevel(checkBox)).Properties.IsLeftButtonPressed)
            {
                checkBox.IsChecked = checkBox.IsChecked == false;
                e.Handled = true;
            }
        }
    }

    public static readonly AttachedProperty<bool> LimitDragDropProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "LimitDragDrop",
            typeof(DragDropExtensions),
            defaultValue: false);
    public static void SetLimitDragDrop(Control element, bool value) =>
        element.SetValue(LimitDragDropProperty, value);
    public static bool GetLimitDragDrop(Control element) => element.GetValue(LimitDragDropProperty);
    private static void OnLimitDragDropChanged(AvaloniaPropertyChangedEventArgs<bool> args)
    {
        if (args.Sender is Control control)
        {
            if (args.NewValue.Value)
            {
                control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
            }
            else
            {
                control.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            }
        }
    }

    private static void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        e.Handled = true;
    }
}
