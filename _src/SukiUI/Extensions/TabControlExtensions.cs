using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace SukiUI.Extensions;

public static class TabControlExtensions
{
    public static readonly AttachedProperty<HorizontalAlignment> HeaderHorizontalAlignmentProperty =
        AvaloniaProperty.RegisterAttached<TabControl, HorizontalAlignment>(
            "HeaderHorizontalAlignment", typeof(TabControlExtensions), defaultValue: HorizontalAlignment.Left);
    public static HorizontalAlignment GetHeaderHorizontalAlignment(TabControl element) =>
        element.GetValue(HeaderHorizontalAlignmentProperty);

    public static void SetHeaderHorizontalAlignment(TabControl element, HorizontalAlignment value) =>
        element.SetValue(HeaderHorizontalAlignmentProperty, value);
}
