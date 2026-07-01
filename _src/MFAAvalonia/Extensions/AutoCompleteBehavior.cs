using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MFAAvalonia.Extensions;

internal class AutoCompleteBehavior : Behavior<AutoCompleteBox>
{
    static AutoCompleteBehavior()
    {
    }

    private bool _justSelectedItem = false;
    // 反射获取_view字段
    private FieldInfo? _viewField;

    protected override void OnAttached()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyUp += OnKeyUp;
            AssociatedObject.GotFocus += OnGotFocus;
            AssociatedObject.DropDownOpening += DropDownOpening;
            AssociatedObject.SelectionChanged += OnSelectionChanged;

            // 获取_view字段（非公共）
            _viewField = typeof(AutoCompleteBox)
                .GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.KeyUp -= OnKeyUp;
            AssociatedObject.GotFocus -= OnGotFocus;
            AssociatedObject.DropDownOpening -= DropDownOpening;
            AssociatedObject.SelectionChanged -= OnSelectionChanged;
        }
        base.OnDetaching();
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (ShouldShowDropdown(sender))
        {
            ShowDropdown();
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (!_justSelectedItem && sender is AutoCompleteBox autoCompleteBox && string.IsNullOrEmpty(autoCompleteBox.Text) && autoCompleteBox.ItemsSource != null && autoCompleteBox.ItemsSource.Cast<object>().Any())
        {
            ShowDropdown();
        }
        _justSelectedItem = false;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _justSelectedItem = true;
    }

    private void DropDownOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var prop = AssociatedObject?.GetType().GetProperty("TextBox", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var tb = (TextBox?)prop?.GetValue(AssociatedObject);
        if (tb is not null && tb.IsReadOnly)
        {
            e.Cancel = true;
            return;
        }
    }

    private void ShowDropdown()
    {
        if (AssociatedObject is not null && !AssociatedObject.IsDropDownOpen)
        {
            typeof(AutoCompleteBox).GetMethod("PopulateDropDown", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(AssociatedObject, new object[]
            {
                AssociatedObject,
                EventArgs.Empty
            });
            typeof(AutoCompleteBox).GetMethod("OpeningDropDown", BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(AssociatedObject, new object[]
            {
                false
            });

            if (!AssociatedObject.IsDropDownOpen)
            {
                var ipc = typeof(AutoCompleteBox).GetField("_ignorePropertyChange", BindingFlags.NonPublic | BindingFlags.Instance);

                if (ipc?.GetValue(AssociatedObject) is bool ipcValue && !ipcValue)
                {
                    ipc.SetValue(AssociatedObject, true);
                }

                AssociatedObject.SetCurrentValue<bool>(AutoCompleteBox.IsDropDownOpenProperty, true);
            }
        }
    }

    private bool ShouldShowDropdown(object? sender)
    {
        if (AssociatedObject == null || _viewField == null)
            return true;

        if (sender is AutoCompleteBox autoCompleteBox && string.IsNullOrEmpty(autoCompleteBox.Text) && autoCompleteBox.ItemsSource != null && autoCompleteBox.ItemsSource.Cast<object>().Any())
        {
            return true;
        }

        // 获取_view字段的值
        var view = _viewField.GetValue(AssociatedObject) as AvaloniaList<object>;

        // 检查过滤后的项目数量
        if (view == null)
            return true;

        // 判断是否显示下拉框：
        // - 如果过滤后没有项目，不显示
        // - 如果只有一个项目且该项目为空，不显示
        if (view.Count == 0 || (view.Count == 1 && view[0] == null))
        {
            return false;
        }


        return true;
    }
}
