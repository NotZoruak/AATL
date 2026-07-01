using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace SukiUI.Controls;

[TemplatePart(Name = ElementTextBox, Type = typeof(TextBox))]
// [TemplatePart(Name = ElementToggleButton, Type = typeof(ToggleButton))]
public class PasswordBox : TemplatedControl
{
    private const string ElementTextBox = "PART_TextBox";

    // private const string ElementToggleButton = "PART_ToggleButton";


    private TextBox _textBox;

    public static readonly StyledProperty<string> PasswordProperty =
        AvaloniaProperty.Register<PasswordBox, string>(
            nameof(Password),
            defaultBindingMode: BindingMode.TwoWay,
            coerce: (sender, value) => value ?? string.Empty);

    public static readonly StyledProperty<bool> IsPasswordVisibleProperty =
        AvaloniaProperty.Register<PasswordBox, bool>(nameof(IsPasswordVisible), false);

    public static readonly StyledProperty<char> PasswordCharProperty =
        AvaloniaProperty.Register<PasswordBox, char>(nameof(PasswordChar), '\u25cf');

    public static readonly RoutedEvent<PasswordChangedEventArgs> PasswordChangedEvent =
        RoutedEvent.Register<PasswordBox, PasswordChangedEventArgs>(
            nameof(PasswordChanged), RoutingStrategies.Bubble);

    public static readonly StyledProperty<string?> WatermarkProperty = AvaloniaProperty.Register<TextBox, string>(nameof(Watermark));

    public string? Watermark
    {
        get => this.GetValue<string>(TextBox.WatermarkProperty);
        set => this.SetValue<string>(TextBox.WatermarkProperty, value);
    }

    public event EventHandler<PasswordChangedEventArgs>? PasswordChanged
    {
        add => AddHandler(PasswordChangedEvent, value);
        remove => RemoveHandler(PasswordChangedEvent, value);
    }

    public string Password
    {
        get => GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public bool IsPasswordVisible
    {
        get => GetValue(IsPasswordVisibleProperty);
        set => SetValue(IsPasswordVisibleProperty, value);
    }

    public char PasswordChar
    {
        get => GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }

    private void TextBox_TextChanged(object? sender, TextChangedEventArgs args)
    {
        var textBox = sender as TextBox;
        if (Password != textBox.Text)
        {
            SetCurrentValue(PasswordProperty, textBox.Text);
        }
    }

// 在 PasswordBox 类中添加
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_textBox != null)
            _textBox.TextChanged -= TextBox_TextChanged;

        base.OnApplyTemplate(e);
        _textBox = e.NameScope.Find<TextBox>(ElementTextBox);

        if (Password != _textBox.Text)
            _textBox.Text = Password;
        // var toggleButton = e.NameScope.Find<ToggleButton>(ElementToggleButton);

        if (_textBox == null)
            throw new InvalidOperationException("Missing required template parts");

        // if (_textBox == null || toggleButton == null)
        //     throw new InvalidOperationException("Missing required template parts");
        //
        if (_textBox != null)
        {
            _textBox.TextChanged += TextBox_TextChanged;
        }
    }
    
    private void RaisePasswordChangeEvents()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_textBox != null && Password != _textBox.Text)
            {
                _textBox.SetCurrentValue(TextBox.TextProperty, Password);
            }
            var textChangedEventArgs = new PasswordChangedEventArgs(PasswordChangedEvent);
            RaiseEvent(textChangedEventArgs);
        }, DispatcherPriority.Normal);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PasswordProperty)
        {
            RaisePasswordChangeEvents();
        }
    }

    public class PasswordChangedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.TextChangedEventArgs" /> class.
        /// </summary>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        public PasswordChangedEventArgs(RoutedEvent? routedEvent)
            : base(routedEvent)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Avalonia.Controls.TextChangedEventArgs" /> class.
        /// </summary>
        /// <param name="routedEvent">The routed event associated with these event args.</param>
        /// <param name="source">The source object that raised the routed event.</param>
        public PasswordChangedEventArgs(RoutedEvent? routedEvent, Interactive? source)
            : base(routedEvent, (object)source)
        {
        }
    }
}
