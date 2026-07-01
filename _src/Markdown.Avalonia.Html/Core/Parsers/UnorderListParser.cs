using HtmlAgilityPack;
using System.Collections.Generic;
using Markdown.Avalonia.Html.Core.Utils;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ColorTextBlock.Avalonia;

namespace Markdown.Avalonia.Html.Core.Parsers
{
    public class UnorderListParser : IBlockTagParser
    {
        public IEnumerable<string> SupportTag => ["ul"];

        bool ITagParser.TryReplace(HtmlNode node, ReplaceManager manager, out IEnumerable<StyledElement> generated)
        {
            var rtn = TryReplace(node, manager, out var list);
            generated = list;
            return rtn;
        }

        public bool TryReplace(HtmlNode node, ReplaceManager manager, out IEnumerable<Control> generated)
        {
            var list = new Grid();
            list.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            list.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            int index = 0;

            foreach (var listItemTag in node.ChildNodes.CollectTag("li"))
            {
                // 解析<li>内部内容
                var itemContent = manager.ParseChildrenAndGroup(listItemTag);
                // 创建<li>对应的容器控件
                var item = CreateItem(itemContent);

                // ########## 新增：解析并应用<li>的样式 ##########
                ApplyLiStyles(listItemTag, item);

                // 创建列表标记（如"・"）
                var markerTxt = new CTextBlock("・");
                markerTxt.TextAlignment = TextAlignment.Right;
                markerTxt.TextWrapping = TextWrapping.NoWrap;
                markerTxt.Classes.Add(global::Markdown.Avalonia.Markdown.ListMarkerClass);

                // 添加到网格
                list.RowDefinitions.Add(new RowDefinition());
                list.Children.Add(markerTxt);
                list.Children.Add(item);

                Grid.SetRow(markerTxt, index);
                Grid.SetColumn(markerTxt, 0);
                Grid.SetRow(item, index);
                Grid.SetColumn(item, 1);

                ++index;
            }

            generated = [list];
            return true;
        }

        private StackPanel CreateItem(IEnumerable<Control> children)
        {
            var panel = new StackPanel()
            {
                Orientation = Orientation.Vertical
            };
            foreach (var child in children)
                panel.Children.Add(child);
            return panel;
        }

        // ########## 新增：应用<li>标签的样式到控件 ##########
        private void ApplyLiStyles(HtmlNode liNode, StackPanel itemPanel)
        {
            // 1. 处理水平对齐
            var alignment = DocUtils.GetHorizontalAlignment(liNode);
            if (alignment.HasValue)
            {
                itemPanel.HorizontalAlignment = alignment.Value;
                // 同步子元素的文本对齐
                foreach (var child in itemPanel.Children)
                {
                    if (child is CTextBlock textBlock)
                    {
                        textBlock.TextAlignment = alignment.Value switch
                        {
                            HorizontalAlignment.Left => TextAlignment.Left,
                            HorizontalAlignment.Right => TextAlignment.Right,
                            HorizontalAlignment.Center => TextAlignment.Center,
                            _ => textBlock.TextAlignment
                        };
                    }
                }
            }

            // 2. 处理前景色（文字颜色）
            var foregroundBrush = DocUtils.GetForegroundColor(liNode);
            if (foregroundBrush != null)
            {
                ApplyForegroundToTextElements(itemPanel, foregroundBrush);
            }

            // 3. 处理字体大小
            var fontSize = DocUtils.GetFontSize(liNode);
            if (fontSize.HasValue)
            {
                ApplyFontSizeToTextElements(itemPanel, fontSize.Value);
            }

            // 4. 处理字体粗细
            var fontWeight = DocUtils.GetFontWeight(liNode);
            if (fontWeight.HasValue)
            {
                ApplyFontWeightToTextElements(itemPanel, fontWeight.Value);
            }

            // 5. 处理字体样式（斜体）
            var fontStyle = DocUtils.GetFontStyle(liNode);
            if (fontStyle.HasValue)
            {
                ApplyFontStyleToTextElements(itemPanel, fontStyle.Value);
            }

            // 6. 处理文本装饰（删除线/下划线）
            var (isStrikethrough, isUnderline) = DocUtils.GetTextDecoration(liNode);
            if (isStrikethrough || isUnderline)
            {
                ApplyTextDecorationToTextElements(itemPanel, isStrikethrough, isUnderline);
            }

            // 7. 处理字体家族
            var fontFamily = DocUtils.GetFontFamily(liNode);
            if (fontFamily != null)
            {
                ApplyFontFamilyToTextElements(itemPanel, fontFamily);
            }

            // 8. 处理背景色
            var backgroundColor = DocUtils.GetBackgroundColor(liNode);
            if (backgroundColor != null)
            {
                ApplyBackgroundColorToTextElements(itemPanel, backgroundColor);
            }
        }

        // ########## 以下为样式应用的工具方法（复用已有逻辑） ##########
        private void ApplyForegroundToTextElements(Control element, IBrush brush)
        {
            if (element is CTextBlock textBlock)
            {
                textBlock.Foreground = brush;
            }
            else if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    ApplyForegroundToTextElements(child, brush);
                }
            }
            else if (element is Border border)
            {
                ApplyForegroundToTextElements(border.Child, brush);
            }
        }

        private void ApplyFontSizeToTextElements(Control element, double fontSize)
        {
            if (element is CTextBlock textBlock)
            {
                textBlock.FontSize = fontSize;
            }
            else if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    ApplyFontSizeToTextElements(child, fontSize);
                }
            }
            else if (element is Border border)
            {
                ApplyFontSizeToTextElements(border.Child, fontSize);
            }
        }

        private void ApplyFontWeightToTextElements(Control element, FontWeight fontWeight)
        {
            if (element is CTextBlock textBlock)
            {
                textBlock.FontWeight = fontWeight;
            }
            else if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    ApplyFontWeightToTextElements(child, fontWeight);
                }
            }
            else if (element is Border border)
            {
                ApplyFontWeightToTextElements(border.Child, fontWeight);
            }
        }

        private void ApplyFontStyleToTextElements(Control element, FontStyle fontStyle)
        {
            if (element is CTextBlock textBlock)
            {
                textBlock.FontStyle = fontStyle;
            }
            else if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    ApplyFontStyleToTextElements(child, fontStyle);
                }
            }
            else if (element is Border border)
            {
                ApplyFontStyleToTextElements(border.Child, fontStyle);
            }
        }

        private void ApplyTextDecorationToTextElements(Control element, bool isStrikethrough, bool isUnderline)
        {
            if (element is CTextBlock textBlock)
            {
                foreach (var cInline in textBlock.Content)
                {
                    if (isStrikethrough)
                        cInline.IsStrikethrough = true;
                    if (isUnderline)
                        cInline.IsUnderline = true;
                }

            }
            else if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    ApplyTextDecorationToTextElements(child, isStrikethrough, isUnderline);
                }
            }
            else if (element is Border border)
            {
                ApplyTextDecorationToTextElements(border.Child, isStrikethrough, isUnderline);
            }
        }

        private void ApplyFontFamilyToTextElements(Control element, FontFamily fontFamily)
        {
            if (element is CTextBlock textBlock)
            {
                textBlock.FontFamily = fontFamily;
            }
            else if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    ApplyFontFamilyToTextElements(child, fontFamily);
                }
            }
            else if (element is Border border)
            {
                ApplyFontFamilyToTextElements(border.Child, fontFamily);
            }
        }

        private void ApplyBackgroundColorToTextElements(Control element, IBrush brush)
        {
            if (element is CTextBlock textBlock)
            {
                textBlock.Background = brush;
            }
            else if (element is Panel panel)
            {
                panel.Background = brush;
                // 递归处理子元素（可选，根据需求）
                foreach (var child in panel.Children)
                {
                    ApplyBackgroundColorToTextElements(child, brush);
                }
            }
            else if (element is Border border)
            {
                border.Background = brush;
                ApplyBackgroundColorToTextElements(border.Child, brush);
            }
        }
    }
}
