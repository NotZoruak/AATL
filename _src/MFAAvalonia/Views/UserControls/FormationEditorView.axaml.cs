using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Linq;

namespace MFAAvalonia.Views.UserControls;

public partial class FormationEditorView : UserControl
{
    public FormationEditorView()
    {
        InitializeComponent();
    }

    /// <summary>实时移除刀装配置中的空白字符，避免保存无效格式</summary>
    private void OnEquipTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || string.IsNullOrEmpty(textBox.Text))
            return;

        var normalized = new string(textBox.Text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (normalized == textBox.Text)
            return;

        var caretIndex = textBox.CaretIndex;
        textBox.Text = normalized;
        textBox.CaretIndex = Math.Min(caretIndex, normalized.Length);
    }
}
