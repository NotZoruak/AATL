using Avalonia;
using Avalonia.Controls;

namespace SukiUI.Extensions;

public static class MenuIconExtensions
{
    // 定义附加属性
    public static readonly AttachedProperty<object?> MenuIconProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, object?>(
            "MenuIcon", 
            typeof(MenuIconExtensions),
            defaultValue: null);

    // Getter 方法
    public static object? GetMenuIcon(AvaloniaObject element) =>
        element.GetValue(MenuIconProperty);

    // Setter 方法
    public static void SetMenuIcon(AvaloniaObject element, object? value) =>
        element.SetValue(MenuIconProperty, value);
}