using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using AvaloniaEdit.Utils;
namespace MFAAvalonia.Extensions;

public static class IconElement
{
    // 图标几何路径
    public static readonly AttachedProperty<Geometry> GeometryProperty =
        AvaloniaProperty.RegisterAttached<Control, Geometry>(
            "Geometry", typeof(IconElement));

    public static Geometry GetGeometry(Control element) =>
        element.GetValue(GeometryProperty);

    public static void SetGeometry(Control element, Geometry value) =>
        element.SetValue(GeometryProperty, value);

    // 图标尺寸
    public static readonly AttachedProperty<double> HeightProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "IconSize", typeof(IconElement), double.NaN);

    public static double GetHeight(Control element) =>
        element.GetValue(HeightProperty);

    public static void SetHeight(Control element, double value) =>
        element.SetValue(HeightProperty, value);

    public static readonly AttachedProperty<double> WidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "IconSize", typeof(IconElement), double.NaN);

    public static double GetWidth(Control element) =>
        element.GetValue(WidthProperty);

    public static void SetWidth(Control element, double value) =>
        element.SetValue(WidthProperty, value);
    
    private static void OnGeometryChanged(AvaloniaPropertyChangedEventArgs<Geometry> args)
    {
        if (args is
            {
                Sender: Button btn,
                NewValue : { HasValue: true, Value: Geometry geometry }
            })
        {
            UpdateButtonContent(btn, geometry);
        }
    }

    static IconElement()
    {
        GeometryProperty.Changed.Subscribe(OnGeometryChanged);
    }

    private static void UpdateButtonContent(Button button, Geometry geometry)
    {
        var pathIcon = new PathIcon
        {
            Data = geometry,
            Width = 12,
            Height = 12,
            [!PathIcon.ForegroundProperty] = new Binding(nameof(Button.Foreground))
            {
                Source = button
            }
        };
        if (!double.IsNaN(GetHeight(button)))
            pathIcon.Height = GetHeight(button);
        if (!double.IsNaN(GetWidth(button)))
            pathIcon.Width = GetWidth(button);
        button.Content = pathIcon;
    }
}

