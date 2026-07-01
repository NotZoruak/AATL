using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using System;

namespace MFAAvalonia.Views.UserControls;

public class PinButton : Button
{
    public static readonly StyledProperty<bool?> IsCheckedProperty =
        AvaloniaProperty.Register<PinButton, bool?>(nameof(IsChecked), false,
            defaultBindingMode: BindingMode.TwoWay);

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public static readonly RoutedEvent<PropertyChangedEventArgs<bool?>> IsCheckedChangedEvent =
        RoutedEvent.Register<PinButton, PropertyChangedEventArgs<bool?>>(
            nameof(IsCheckedChanged),
            RoutingStrategies.Bubble);

    public event EventHandler<PropertyChangedEventArgs<bool?>>? IsCheckedChanged
    {
        add => AddHandler(IsCheckedChangedEvent, value);
        remove => RemoveHandler(IsCheckedChangedEvent, value);
    }

    protected virtual void OnIsCheckedChanged(PropertyChangedEventArgs<bool?> e)
    {
        RaiseEvent(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsCheckedProperty)
        {
            var newValue = change.GetNewValue<bool?>();
            var oldValue = change.GetOldValue<bool?>();
            UpdatePseudoClasses(newValue);
            
            OnIsCheckedChanged(new PropertyChangedEventArgs<bool?>(oldValue, newValue, IsCheckedChangedEvent));
        }
    }

    private void UpdatePseudoClasses(bool? isChecked)
    {
        PseudoClasses.Set(":checked", isChecked == true);
        PseudoClasses.Set(":unchecked", isChecked == false);
        PseudoClasses.Set(":indeterminate", isChecked == null);
    }
    // Constructor
    public PinButton()
    {
        // Set the default content to 📌
        Content = "📌";
        Click += (_, _) => { IsChecked = !IsChecked; };
    }
    
    public class PropertyChangedEventArgs<T>(T oldValue, T newValue, RoutedEvent routedEvent) 
        : RoutedEventArgs(routedEvent)
    {
        public T OldValue { get; } = oldValue;
        public T NewValue { get; } = newValue;
    }
}
