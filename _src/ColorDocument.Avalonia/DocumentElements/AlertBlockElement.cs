using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorDocument.Avalonia.DocumentElements
{
    /// <summary>
    /// GitHub-style alert type enumeration
    /// </summary>
    public enum AlertType
    {
        Note,
        Tip,
        Important,
        Warning,
        Caution
    }

    /// <summary>
    /// The document element for GitHub-style alert blocks (e.g., [!TIP], [!NOTE], [!WARNING], etc.)
    /// </summary>
    public class AlertBlockElement : DocumentElement
    {
        private Lazy<Border> _block;
        private EnumerableEx<DocumentElement> _children;
        private SelectionList? _prevSelection;
        private AlertType _alertType;

        public override Control Control => _block.Value;
        public override IEnumerable<DocumentElement> Children => _children;
        public AlertType AlertType => _alertType;

        public AlertBlockElement(IEnumerable<DocumentElement> child, AlertType alertType)
        {
            _alertType = alertType;
            _block = new Lazy<Border>(Create);
            _children = child.ToEnumerable();
        }

        private Border Create()
        {
            // Create icon path
            var iconPath = new Path
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 8, 0)
            };

            // Set icon and color based on alert type
            string iconData;
            string colorHex;
            string alertTitle;
            string alertClassName;

            switch (_alertType)
            {
                case AlertType.Tip:
                    // Lightbulb icon
                    iconData = "M12 2C8.13 2 5 5.13 5 9c0 2.38 1.19 4.47 3 5.74V17c0 .55.45 1 1 1h6c.55 0 1-.45 1-1v-2.26c1.81-1.27 3-3.36 3-5.74 0-3.87-3.13-7-7-7zM9 21c0 .55.45 1 1 1h4c.55 0 1-.45 1-1v-1H9v1z";
                    colorHex = "#1a7f37";
                    alertTitle = "Tip";
                    alertClassName = ClassNames.AlertTipClass;
                    break;
                case AlertType.Important:
                    // Exclamation mark in circle icon
                    iconData = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z";
                    colorHex = "#8250df";
                    alertTitle = "Important";
                    alertClassName = ClassNames.AlertImportantClass;
                    break;
                case AlertType.Warning:
                    // Warning triangle icon
                    iconData = "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z";
                    colorHex = "#9a6700";
                    alertTitle = "Warning";
                    alertClassName = ClassNames.AlertWarningClass;
                    break;
                case AlertType.Caution:
                    // Stop/danger icon
                    iconData = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm5 11H7v-2h10v2z";
                    colorHex = "#cf222e";
                    alertTitle = "Caution";
                    alertClassName = ClassNames.AlertCautionClass;
                    break;
                case AlertType.Note:
                default:
                    // Info icon
                    iconData = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z";
                    colorHex = "#0969da";
                    alertTitle = "Note";
                    alertClassName = ClassNames.AlertNoteClass;
                    break;
            }

            var alertColor = Color.Parse(colorHex);
            var alertBrush = new SolidColorBrush(alertColor);

            iconPath.Data = Geometry.Parse(iconData);
            iconPath.Fill = alertBrush;

            // Create title text
            var titleText = new TextBlock
            {
                Text = alertTitle,
                FontWeight = FontWeight.SemiBold,
                Foreground = alertBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };

            // Create header panel (icon + title)
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };
            headerPanel.Children.Add(iconPath);
            headerPanel.Children.Add(titleText);

            // Create content panel
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            foreach (var child in Children)
                contentPanel.Children.Add(child.Control);

            // Create main panel
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            mainPanel.Classes.Add(alertClassName);
            mainPanel.Children.Add(headerPanel);
            mainPanel.Children.Add(contentPanel);

            // Create border
            var border = new Border
            {
                BorderBrush = alertBrush,
                BorderThickness = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 8, 0, 8),
                Child = mainPanel
            };
            border.Classes.Add(alertClassName);

            return border;
        }

        public override void Select(Point from, Point to)
        {
            var selection = SelectionUtil.SelectVertical(Control, _children, from, to);

            if (_prevSelection is not null)
            {
                foreach (var ps in _prevSelection)
                {
                    if (!selection.Any(cs => ReferenceEquals(cs, ps)))
                    {
                        ps.UnSelect();
                    }
                }
            }

            _prevSelection = selection;
        }

        public override void UnSelect()
        {
            foreach (var child in _children)
                child.UnSelect();
        }

        public override void ConstructSelectedText(StringBuilder builder)
        {
            if (_prevSelection is null)
                return;

            var preLen = builder.Length;

            foreach (var para in _prevSelection)
            {
                para.ConstructSelectedText(builder);

                if (preLen == builder.Length)
                    continue;

                if (builder[builder.Length - 1] != '\n')
                    builder.Append('\n');
            }
        }
    }
}
