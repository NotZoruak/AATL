using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Xaml.Interactivity;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions;
using MFAAvalonia.Extensions.MaaFW;
using MFAAvalonia.Helper.ValueType;
using MFAAvalonia.Models;
using MFAAvalonia.ViewModels.Pages;
using MFAAvalonia.ViewModels.UsersControls;
using MFAAvalonia.ViewModels.UsersControls.Settings;
using MFAAvalonia.Views.UserControls;
using MFAAvalonia.Views.UserControls.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Lang.Avalonia.MarkupExtensions;
using SukiUI.Extensions;
using Action = System.Action;

namespace MFAAvalonia.Helper;

public class TaskOptionGenerator(TaskQueueViewModel viewModel, Action saveConfigurationAction)
{
    /// <summary>复制暂存的编队预设，供「粘贴」使用</summary>
    private FormationPreset? _formationClipboard;

    public void GeneratePanelContent(StackPanel panel, DragItemViewModel dragItem)
    {
        // 检测是否为特殊任务，如果是则生成特殊任务选项面板
        var entry = dragItem.InterfaceItem?.Entry;
        if (!string.IsNullOrEmpty(entry) && AddTaskDialogViewModel.SpecialActionNames.Contains(entry))
        {
            GenerateSpecialTaskPanelContent(panel, dragItem);
            return;
        }

        AddRepeatOption(panel, dragItem);

        if (dragItem.InterfaceItem?.Option != null)
        {
            AddOptionsWithCheckboxGrid(panel, dragItem, dragItem.InterfaceItem.Option.ToList());
        }

        if (dragItem.InterfaceItem?.Advanced != null)
        {
            foreach (var option in dragItem.InterfaceItem.Advanced.ToList())
            {
               AddAdvancedOption(panel, option);
            }
        }
    }

    public void GenerateCommonPanelContent(StackPanel panel, DragItemViewModel dragItem)
    {
        // 检测是否为特殊任务，如果是则生成特殊任务选项面板
        var entry = dragItem.InterfaceItem?.Entry;
        if (!string.IsNullOrEmpty(entry) && AddTaskDialogViewModel.SpecialActionNames.Contains(entry))
        {
            GenerateSpecialTaskPanelContent(panel, dragItem);
            return;
        }

        AddRepeatOption(panel, dragItem);

        if (dragItem.InterfaceItem?.Option != null)
        {
            AddOptionsWithCheckboxGrid(panel, dragItem, dragItem.InterfaceItem.Option.ToList());
        }
    }

    /// <summary>
    /// 遍历选项列表，将连续 checkbox 收集到一个两列 UniformGrid 中，非 checkbox 直接添加
    /// </summary>
    private void AddOptionsWithCheckboxGrid(StackPanel panel, DragItemViewModel dragItem, List<MaaInterface.MaaInterfaceSelectOption> options)
    {
        var checkboxBatch = new List<(MaaInterface.MaaInterfaceSelectOption, MaaInterface.MaaInterfaceOption)>();

        void FlushBatch()
        {
            if (checkboxBatch.Count == 0) return;
            var grid = new UniformGrid
            {
                Columns = 2,
                Margin = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            foreach (var (opt, ifOpt) in checkboxBatch)
            {
                grid.Children.Add(CreateCheckboxControl(opt, ifOpt, dragItem));
            }
            grid.SizeChanged += (sender, _) =>
            {
                if (sender is not UniformGrid ug || ug.Children.Count == 0) return;
                double maxChildWidth = 0;
                foreach (var child in ug.Children)
                {
                    if (child is Control c)
                    {
                        c.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        if (c.DesiredSize.Width > maxChildWidth)
                            maxChildWidth = c.DesiredSize.Width;
                    }
                }
                if (maxChildWidth <= 0) return;
                var available = ug.Bounds.Width - ug.Margin.Left - ug.Margin.Right;
                var cols = Math.Max(1, (int)(available / maxChildWidth));
                ug.Columns = cols;
            };
            panel.Children.Add(grid);
            checkboxBatch.Clear();
        }

        foreach (var option in options)
        {
            if (MaaProcessor.Interface?.Option?.TryGetValue(option.Name ?? string.Empty, out var interfaceOption) != true)
                continue;

            if (interfaceOption.IsCheckbox)
            {
                checkboxBatch.Add((option, interfaceOption));
            }
            else
            {
                FlushBatch();
                AddOption(panel, option, dragItem);
            }
        }
        FlushBatch();
    }

    public void GenerateAdvancedPanelContent(StackPanel panel, DragItemViewModel dragItem)
    {
        if (dragItem.InterfaceItem?.Advanced != null)
        {
            foreach (var option in dragItem.InterfaceItem.Advanced.ToList())
            {
                AddAdvancedOption(panel, option);
            }
        }
    }

    public void GenerateGlobalPanelContent(StackPanel panel)
    {
        // 刀解/合成许可名单：全局强制配置入口行，齿轮进入子页面编辑
        panel.Children.Add(CreateAllowListEntryRow());

        var globalOptions = MaaProcessor.Interface?.GlobalSelectOptions;
        if (globalOptions != null)
        {
            foreach (var option in globalOptions)
            {
                AddOption(panel, option, null!);
            }
        }

        // 刀剑掉落播报放在全局设置末尾，开关控制总启用状态，齿轮进入名单编辑页
        panel.Children.Add(CreateSwordDropNotificationRow());
    }

    /// <summary>全局设置 - 刀剑掉落播报入口行。</summary>
    private Control CreateSwordDropNotificationRow()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Margin = new Thickness(10, 6, 10, 6),
        };

        var labelPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var titleBlock = new TextBlock
        {
            Text = "刀剑掉落播报",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiLowText"));
        labelPanel.Children.Add(titleBlock);

        var gearIcon = new FluentIcons.Avalonia.Fluent.FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Settings,
            IconSize = FluentIcons.Common.IconSize.Size16,
            Width = 16,
            Height = 16,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(gearIcon, "设置刀剑掉落播报名单");
        gearIcon.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            viewModel.SubPageTitle = "刀剑掉落播报";
            viewModel.SubPageContent = new SwordDropNotificationUserControl();
            viewModel.IsSubPageOpen = true;
        };
        labelPanel.Children.Add(gearIcon);
        Grid.SetColumn(labelPanel, 0);
        grid.Children.Add(labelPanel);

        var toggle = new ToggleSwitch
        {
            Classes = { "Switch" },
            IsChecked = ConfigurationManager.Current.GetValue(ConfigurationKeys.SwordDropNotificationEnabled, false),
            VerticalAlignment = VerticalAlignment.Top,
        };
        toggle.IsCheckedChanged += (_, _) => ConfigurationManager.Current.SetValue(
            ConfigurationKeys.SwordDropNotificationEnabled, toggle.IsChecked == true);
        Grid.SetColumn(toggle, 2);
        grid.Children.Add(toggle);

        return grid;
    }

    /// <summary>全局设置 - 刀解/合成许可名单入口行（标签 + 齿轮，齿轮进入子页面）</summary>
    private Control CreateAllowListEntryRow()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Margin = new Thickness(10, 6, 10, 6),
        };

        var labelPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var titleBlock = new TextBlock
        {
            Text = "刀解/合成许可名单",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiLowText"));
        labelPanel.Children.Add(titleBlock);
        Grid.SetColumn(labelPanel, 0);
        grid.Children.Add(labelPanel);

        var gearIcon = new FluentIcons.Avalonia.Fluent.FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Settings,
            IconSize = FluentIcons.Common.IconSize.Size16,
            Width = 16,
            Height = 16,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(gearIcon, "设置许可名单");
        gearIcon.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            viewModel.SubPageTitle = "刀解/合成许可名单";
            viewModel.SubPageContent = new AllowListUserControl();
            viewModel.IsSubPageOpen = true;
        };
        labelPanel.Children.Add(gearIcon);

        return grid;
    }

    public void GenerateResourceOptionPanelContent(StackPanel panel, DragItemViewModel dragItem)
    {
        if (dragItem.ResourceItem?.SelectOptions == null)
            return;

        // 收集所有子选项名称（这些选项不应该在顶级显示）
        var subOptionNames = new HashSet<string>();
        foreach (var selectOption in dragItem.ResourceItem.SelectOptions)
        {
            if (MaaProcessor.Interface?.Option?.TryGetValue(selectOption.Name ?? string.Empty, out var interfaceOption) == true)
            {
                if (interfaceOption.Cases != null)
                {
                    foreach (var caseOption in interfaceOption.Cases)
                    {
                        if (caseOption.Option != null)
                        {
                            foreach (var subOptionName in caseOption.Option)
                            {
                                subOptionNames.Add(subOptionName);
                            }
                        }
                    }
                }
            }
        }

        foreach (var selectOption in dragItem.ResourceItem.SelectOptions)
        {
            if (subOptionNames.Contains(selectOption.Name ?? string.Empty))
                continue;

            AddOption(panel, selectOption, dragItem);
        }
    }

    private void AddRepeatOption(Panel panel, DragItemViewModel source)
    {
        if (source.InterfaceItem is not { Repeatable: true }) return;
        
        var grid = CreateBaseGrid();

        var textBlock = CreateLabelText("RepeatOption");
        Grid.SetColumn(textBlock, 0);
        grid.Children.Add(textBlock);

        var numericUpDown = new NumericUpDown
        {
            Value = source.InterfaceItem.RepeatCount ?? 1,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            Increment = 1,
            Minimum = -1,
        };
        
        BindIdleEnabled(numericUpDown);
        
        numericUpDown.ValueChanged += (_, _) =>
        {
            source.InterfaceItem.RepeatCount = Convert.ToInt32(numericUpDown.Value);
            saveConfigurationAction();
        };
        
        Grid.SetColumn(numericUpDown, 1);
        AddResponsiveBehavior(grid, textBlock, numericUpDown);
        
        grid.Children.Add(numericUpDown);
        panel.Children.Add(grid);
    }

    private void AddOption(Panel panel, MaaInterface.MaaInterfaceSelectOption option, DragItemViewModel source)
    {
        if (MaaProcessor.Interface?.Option?.TryGetValue(option.Name ?? string.Empty, out var interfaceOption) != true) return;

        // 过滤：根据 option.controller / option.resource 隐藏不适用项（任务 11）
        if (!IsOptionApplicable(interfaceOption)) return;

        Control control;

        if (interfaceOption.IsInput)
        {
            control = CreateInputControl(option, interfaceOption);
        }
        else if (interfaceOption.IsCheckbox)
        {
            // checkbox 类型：CheckBox 列表（方框+文字形式）
            control = CreateCheckboxControl(option, interfaceOption, source);
        }
        else if ((interfaceOption.IsSwitch && interfaceOption.Cases.ShouldSwitchButton(out var yes, out var no)) ||
                 interfaceOption.Cases.ShouldSwitchButton(out yes, out no)) // Try both conditions with/without type checking logic
        {
            control = CreateToggleControl(option, yes, no, interfaceOption, source);
        }
        else
        {
            control = CreateComboBoxControl(option, interfaceOption, source);
        }

        panel.Children.Add(control);
    }

    /// <summary>
    /// 检查 option 是否适用于当前控制器和资源（任务 11）
    /// </summary>
    /// <summary>判断配置项是否有子选项（任意 case 包含 option 字段）</summary>
    private static bool HasSubOptions(MaaInterface.MaaInterfaceOption interfaceOption)
    {
        return interfaceOption.Cases?.Any(c => c.Option is { Count: > 0 }) == true;
    }

    /// <summary>在标签面板末尾追加齿轮图标，表示该项有子选项</summary>
    private void AppendGearIcon(StackPanel labelPanel, MaaInterface.MaaInterfaceSelectOption option, MaaInterface.MaaInterfaceOption interfaceOption, DragItemViewModel source)
    {
        var gearIcon = new FluentIcons.Avalonia.Fluent.FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Settings,
            IconSize = FluentIcons.Common.IconSize.Size16,
            Width = 16,
            Height = 16,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(gearIcon, "含有子选项");

        gearIcon.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            var subPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 0) };
            // 仅多个带子选项的 case 时渲染 case 标题；单选项场景标题与返回键旁重复，不渲染
            var caseCount = interfaceOption.Cases?.Count(c => c.Option is { Count: > 0 }) ?? 0;
            foreach (var caseOption in interfaceOption.Cases ?? [])
            {
                if (caseOption.Option == null || caseOption.Option.Count == 0) continue;
                if (caseCount > 1 && !string.IsNullOrWhiteSpace(caseOption.DisplayName))
                {
                    subPanel.Children.Add(new TextBlock
                    {
                        Text = caseOption.DisplayName,
                        FontSize = 14,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 2, 0, 2),
                    });
                }
                // 子选项垂直排列，每项独占一行并横向拉伸，与任务选项面板的下拉框尺寸一致
                var subWrap = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
                foreach (var subName in caseOption.Option)
                {
                    if (MaaProcessor.Interface?.Option?.TryGetValue(subName, out var subDef) != true) continue;
                    option.SubOptions ??= new List<MaaInterface.MaaInterfaceSelectOption>();
                    var subSelect = option.SubOptions.FirstOrDefault(o => o.Name == subName)
                        ?? CreateDefaultSelectOption(subName);
                    if (!option.SubOptions.Contains(subSelect))
                        option.SubOptions.Add(subSelect);
                    var savedDesc = subDef.Description;
                    subDef.Description = null;
                    var itemBox = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
                    AddSubOption(itemBox, subSelect, source);
                    subDef.Description = savedDesc;
                    var subDesc = GetTooltipText(savedDesc, subDef.Document);
                    if (!string.IsNullOrWhiteSpace(subDesc))
                    {
                        itemBox.Children.Add(new TextBlock
                        {
                            Text = subDesc,
                            FontSize = 12,
                            Foreground = Brushes.Gray,
                            Margin = new Thickness(0, 2, 0, 0),
                            TextWrapping = TextWrapping.Wrap,
                        });
                    }
                    subWrap.Children.Add(itemBox);
                }
                subPanel.Children.Add(subWrap);
            }
            viewModel.SubPageTitle = interfaceOption.DisplayName;
            viewModel.SubPageContent = subPanel;
            viewModel.IsSubPageOpen = true;
        };

        labelPanel.Children.Add(gearIcon);
    }

    /// <summary>
    /// 一键日课 - 预设部队开关的齿轮：打开自定编队预设选择页（互斥勾选 + 编辑 + 新增），
    /// preset_id 写入该任务选项的 Data 字典，与其他自定编队任务互不影响
    /// </summary>
    private void AppendDailyPresetGearIcon(StackPanel labelPanel, MaaInterface.MaaInterfaceSelectOption option, DragItemViewModel source)
    {
        var gearIcon = new FluentIcons.Avalonia.Fluent.FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Settings,
            IconSize = FluentIcons.Common.IconSize.Size16,
            Width = 16,
            Height = 16,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(gearIcon, "选择预设");

        gearIcon.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            var subPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 0) };
            RenderFormationPresets(subPanel,
                () => GetDailyPresetId(option),
                id =>
                {
                    option.Data ??= new Dictionary<string, string?>();
                    option.Data["preset_id"] = id.ToString();
                    saveConfigurationAction();
                },
                source);
            viewModel.SubPageTitle = "选择预设部队";
            viewModel.SubPageContent = subPanel;
            viewModel.IsSubPageOpen = true;
        };

        labelPanel.Children.Add(gearIcon);
    }

    /// <summary>一键日课 - 预设部队选项名（interface.json 中定义，齿轮进入自定编队预设选择页）</summary>
    private static bool IsDailyPresetOption(string? optionName)
        => optionName == "D_启用预设部队";

    /// <summary>一键日课 - 读取预设部队选项保存的 preset_id</summary>
    private static int GetDailyPresetId(MaaInterface.MaaInterfaceSelectOption option)
    {
        if (option.Data != null && option.Data.TryGetValue("preset_id", out var raw)
            && int.TryParse(raw, out var id))
            return id;
        return 0;
    }

    private bool IsOptionApplicable(MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var currentControllerName = MaaInterfaceActivationHelper.ResolveControllerName(
            MaaProcessor.Interface,
            viewModel.CurrentController) ?? viewModel.CurrentController.ToJsonKey();

        return MaaInterfaceActivationHelper.IsOptionApplicable(
            MaaProcessor.Interface,
            interfaceOption,
            currentControllerName,
            viewModel.CurrentResource);
    }

    /// <summary>
    /// 创建 checkbox 类型的 CheckBox 控件
    /// </summary>
    private Control CreateCheckboxControl(MaaInterface.MaaInterfaceSelectOption option, MaaInterface.MaaInterfaceOption interfaceOption, DragItemViewModel source)
    {
        interfaceOption.InitializeIcon();

        // 初始化 SelectedCases（任务 9）
        option.SelectedCases ??= new List<string>(interfaceOption.DefaultCases ?? new List<string>());

        // Sub-options container（必须在 isSingleCase 块之前声明，
        // 因为单 case 的 CheckBox 事件处理会引用 UpdateSubOptions）
        var subOptionsContainer = new StackPanel();

        var isSingleCase = interfaceOption.Cases?.Count == 1;

        StackPanel container;
        Panel wrapPanel;

        if (isSingleCase)
        {
            // 单个勾选框：CheckBox + 文字在右侧
            container = new StackPanel { Margin = new Thickness(10, 6, 10, 6), Spacing = 4 };

            var caseOption = interfaceOption.Cases![0];
            caseOption.InitializeDisplayName();
            var caseName = caseOption.Name ?? string.Empty;
            var isChecked = option.SelectedCases.Contains(caseName);

            var checkBox = new CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            checkBox.Bind(CheckBox.ContentProperty, new ResourceBindingWithFallback(interfaceOption.DisplayName, interfaceOption.Name));

            BindIdleEnabled(checkBox);

            var capturedCaseName = caseName;
            checkBox.IsCheckedChanged += (_, _) =>
            {
                if (checkBox.IsChecked == true)
                {
                    if (!option.SelectedCases.Contains(capturedCaseName))
                        option.SelectedCases.Add(capturedCaseName);
                }
                else
                {
                    option.SelectedCases.Remove(capturedCaseName);
                }
                UpdateSubOptions();
                saveConfigurationAction();
            };

            // 齿轮图标（子选项入口）
            if (HasSubOptions(interfaceOption))
            {
                var gearRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                gearRow.Children.Add(checkBox);
                AppendGearIcon(gearRow, option, interfaceOption, source);
                container.Children.Add(gearRow);
            }
            else
            {
                container.Children.Add(checkBox);
            }

            // TooltipBlock for option-level description
            if (!string.IsNullOrWhiteSpace(GetTooltipText(interfaceOption.Description, interfaceOption.Document)))
            {
                var docBlock = new TooltipBlock();
                docBlock.Bind(TooltipBlock.TooltipTextProperty, new ResourceBinding(GetTooltipText(interfaceOption.Description, interfaceOption.Document)!));
                docBlock.Margin = new Thickness(0, 0, 0, 0);
                container.Children.Add(docBlock);
            }

            wrapPanel = new UniformGrid { Columns = 2 }; // 不使用，但后续代码需要变量存在
        }
        else
        {
            // 多个勾选框：WrapPanel 自适应换行
            container = new StackPanel { Margin = new Thickness(10, 10, 10, 6), Spacing = 4 };

            var header = (StackPanel)CreateOptionHeader(interfaceOption);
            header.Margin = new Thickness(0);
            container.Children.Add(header);
            if (HasSubOptions(interfaceOption))
                AppendGearIcon(header, option, interfaceOption, source);

            wrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        // Sub-option update logic for checkbox（与 select/switch 不同，需要显示所有已勾选 case 的子选项）
        void UpdateSubOptions()
        {
            subOptionsContainer.Children.Clear();
            if (HasSubOptions(interfaceOption)) return; // 有齿轮入口，子选项只在子页面显示
            if (interfaceOption.Cases == null) return;

            option.SubOptions ??= new List<MaaInterface.MaaInterfaceSelectOption>();
            var selectedCases = option.SelectedCases ?? new List<string>();

            foreach (var caseItem in interfaceOption.Cases)
            {
                if (caseItem.Name == null || !selectedCases.Contains(caseItem.Name)) continue;
                if (caseItem.Option == null || caseItem.Option.Count == 0) continue;

                foreach (var subOptionName in caseItem.Option)
                {
                    var existing = option.SubOptions.FirstOrDefault(o => o.Name == subOptionName);
                    if (existing == null)
                    {
                        existing = CreateDefaultSelectOption(subOptionName);
                        option.SubOptions.Add(existing);
                    }
                    AddSubOption(subOptionsContainer, existing, source);
                }
            }
        }

        if (!isSingleCase && interfaceOption.Cases != null)
        {
            foreach (var caseOption in interfaceOption.Cases)
            {
                caseOption.InitializeDisplayName();
                var caseName = caseOption.Name ?? string.Empty;
                var isChecked = option.SelectedCases.Contains(caseName);

                var checkBox = new CheckBox
                {
                    IsChecked = isChecked,
                    Margin = new Thickness(2, 4, 6, 4),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                checkBox.Bind(CheckBox.ContentProperty, new ResourceBindingWithFallback(caseOption.DisplayName, caseOption.Name));

                BindIdleEnabled(checkBox);

                var capturedCaseName = caseName;
                checkBox.IsCheckedChanged += (_, _) =>
                {
                    if (checkBox.IsChecked == true)
                    {
                        if (!option.SelectedCases.Contains(capturedCaseName))
                            option.SelectedCases.Add(capturedCaseName);
                    }
                    else
                    {
                        option.SelectedCases.Remove(capturedCaseName);
                    }
                    UpdateSubOptions();
                    saveConfigurationAction();
                };

                wrapPanel.Children.Add(checkBox);
            }
        }

        // 首次布局后测量所有按钮自然宽度，取最大值统一设置 MinWidth（等宽效果）
        if (wrapPanel.Children.Count > 0)
        {
            wrapPanel.LayoutUpdated += EqualizeOnce;
            void EqualizeOnce(object? s, EventArgs ev)
            {
                wrapPanel.LayoutUpdated -= EqualizeOnce;
                double maxWidth = 0;
                foreach (var child in wrapPanel.Children)
                {
                    if (child is CheckBox cb)
                    {
                        cb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        if (cb.DesiredSize.Width > maxWidth)
                            maxWidth = cb.DesiredSize.Width;
                    }
                }
                if (maxWidth > 0)
                {
                    foreach (var child in wrapPanel.Children)
                    {
                        if (child is CheckBox cb)
                            cb.MinWidth = maxWidth;
                    }
                }
            }
        }

        if (!isSingleCase)
            container.Children.Add(wrapPanel);

        // Initialize sub-options for default checked cases
        UpdateSubOptions();

        // Enhanced Sub-option visualization
        var subOptionsBorder = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Background = Brushes.Transparent,
            Margin = new Thickness(24, 0, 0, 4),
            Padding = new Thickness(8, 0, 0, 0),
            Child = subOptionsContainer
        };
        subOptionsBorder.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("SukiPrimaryColor"));
        subOptionsBorder.Bind(Visual.IsVisibleProperty, new Binding("Children.Count")
        {
            Source = subOptionsContainer,
            Converter = new FuncValueConverter<int, bool>(count => count > 0)
        });

        container.Children.Add(subOptionsBorder);
        return container;
    }

    private Control CreateInputControl(MaaInterface.MaaInterfaceSelectOption option, MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var hasOptionDescription = !string.IsNullOrWhiteSpace(GetTooltipText(interfaceOption.Description, interfaceOption.Document));
        var isSingleInput = interfaceOption.Inputs?.Count == 1;
        
        // 单输入且有 option description 时，需要显示 header，所以使用与多输入相同的 margin
        var needsHeader = !isSingleInput || hasOptionDescription;
        
        var container = new StackPanel
        {
            Margin = needsHeader ? new Thickness(10, 3, 10, 3) : new Thickness(0),
            Spacing = 4
        };

        option.Data ??= new Dictionary<string, string?>();
        if (interfaceOption.Inputs == null || interfaceOption.Inputs.Count == 0) return container;

        interfaceOption.InitializeIcon();

        // Header for multi-input options OR single input with option description
        if (needsHeader)
        {
            container.Children.Add(CreateOptionHeader(interfaceOption));
        }

        foreach (var input in interfaceOption.Inputs)
        {
            if (string.IsNullOrEmpty(input.Name)) continue;

            if (!option.Data.TryGetValue(input.Name, out var currentValue) || currentValue == null)
            {
                currentValue = input.Default ?? string.Empty;
                option.Data[input.Name] = currentValue;
            }

            var pipelineType = input.PipelineType?.ToLower() ?? "string";

            if (pipelineType == "bool")
            {
                container.Children.Add(CreateBoolInputControl(input, currentValue, option, interfaceOption));
            }
            else
            {
                container.Children.Add(CreateStringInputControl(input, currentValue, option, interfaceOption));
            }
        }

        return container;
    }

    private Control CreateStringInputControl(
        MaaInterface.MaaInterfaceOptionInput input, 
        string currentValue, 
        MaaInterface.MaaInterfaceSelectOption option, 
        MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var grid = CreateBaseGrid();
        
        // Adjust margin based on whether header is shown
        var hasOptionDescription = !string.IsNullOrWhiteSpace(GetTooltipText(interfaceOption.Description, interfaceOption.Document));
        var needsHeader = interfaceOption.Inputs.Count > 1 || hasOptionDescription;
        
        if (needsHeader)
        {
            // When header is shown, container already has margin, so grid needs no extra left/right margin
            grid.Margin = new Thickness(0, 3, 0, 3);
        }
        else
        {
            // Single input without header needs its own margin
            grid.Margin = new Thickness(10, 6, 10, 6);
        }

        // TextBox
        var displayValue = currentValue == MaaInterface.MaaInterfaceOption.ExplicitNullMarker ? "null" : currentValue;
        var textBox = new TextBox
        {
            MinWidth = 200,
            Margin = new Thickness(0, 2, 0, 2),
            BorderThickness = new Thickness(1),
            Text = displayValue,
            VerticalAlignment = VerticalAlignment.Center
        };
        textBox.Bind(TextBox.BorderBrushProperty, new DynamicResourceExtension("SukiControlBorderBrush"));
        
        if (!string.IsNullOrWhiteSpace(input.PatternMsg))
            textBox.Bind(TextBox.WatermarkProperty, new ResourceBinding(input.PatternMsg));
        
        BindIdleEnabled(textBox);

        // Events
        textBox.TextChanged += (_, _) => HandleStringInputChange(textBox, input, option, interfaceOption);
        
        // Initial setup
        HandleStringInputChange(textBox, input, option, interfaceOption, true); 

        // Label Panel
        var labelPanel = CreateLabelPanel(input.DisplayName, input.Name, input.Description);

        // Icon (Show only if single input WITHOUT header, because header already has icon)
        if (!needsHeader)
        {
            var icon = CreateIcon(interfaceOption);
            icon.Margin = new Thickness(10, 0, 6, 0); // specific margin for single input layout
            labelPanel.Children.Insert(0, icon);
        }

        Grid.SetColumn(labelPanel, 0);
        Grid.SetColumn(textBox, 1);
        
        AddResponsiveBehavior(grid, labelPanel, textBox);
        
        grid.Children.Add(labelPanel);
        grid.Children.Add(textBox);

        return grid;
    }

    private Control CreateBoolInputControl(
        MaaInterface.MaaInterfaceOptionInput input,
        string currentValue,
        MaaInterface.MaaInterfaceSelectOption option,
        MaaInterface.MaaInterfaceOption interfaceOption)
    {
        // Adjust margin based on whether header is shown
        var hasOptionDescription = !string.IsNullOrWhiteSpace(GetTooltipText(interfaceOption.Description, interfaceOption.Document));
        var needsHeader = interfaceOption.Inputs.Count > 1 || hasOptionDescription;
        
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = needsHeader ? new Thickness(0, 3, 0, 3) : new Thickness(10, 6, 10, 6)
        };

        bool isChecked = currentValue.Equals("true", StringComparison.OrdinalIgnoreCase) || currentValue == "1";

        var toggleSwitch = new ToggleSwitch
        {
            IsChecked = isChecked,
            Classes = { "Switch" },
            MaxHeight = 60,
            MaxWidth = 100,
            Margin = new Thickness(0, 2, 0, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        BindIdleEnabled(toggleSwitch);
        toggleSwitch.Bind(ToolTip.TipProperty, new ResourceBindingWithFallback(input.DisplayName, input.Name));

        var fieldName = input.Name;
        toggleSwitch.IsCheckedChanged += (_, _) =>
        {
            var boolValue = toggleSwitch.IsChecked == true;
            option.Data[fieldName] = boolValue.ToString().ToLower();
            UpdatePipeline(option, interfaceOption);
            saveConfigurationAction();
        };

        var labelPanel = CreateLabelPanel(input.DisplayName, input.Name, input.Description);
        labelPanel.Margin = new Thickness(10, 0, 5, 0);

        Grid.SetColumn(labelPanel, 0);
        Grid.SetColumn(toggleSwitch, 2);
        
        grid.Children.Add(labelPanel);
        grid.Children.Add(toggleSwitch);

        return grid;
    }

    private Control CreateToggleControl(
        MaaInterface.MaaInterfaceSelectOption option,
        int yesValue,
        int noValue,
        MaaInterface.MaaInterfaceOption interfaceOption,
        DragItemViewModel source)
    {
        var wrapper = new StackPanel();

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(10, 6, 10, 6)
        };
        
        interfaceOption.InitializeIcon();

        var toggleSwitch = new ToggleSwitch
        {
            IsChecked = option.Index == yesValue,
            Classes = { "Switch" },
            MaxHeight = 60,
            MaxWidth = 100,
            Margin = new Thickness(0, 4, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = option.Name
        };

        BindIdleEnabled(toggleSwitch);
        toggleSwitch.Bind(ToolTip.TipProperty, new ResourceBindingWithFallback(option.DisplayName, option.Name));
        
        // Sub-options container
        var subOptionsContainer = new StackPanel();

        // Sub-option update logic
        void UpdateSubOptions(int index)
        {
            subOptionsContainer.Children.Clear();
            if (HasSubOptions(interfaceOption)) return; // 有齿轮入口，子选项只在子页面显示
            if (interfaceOption.Cases == null || index < 0 || index >= interfaceOption.Cases.Count) return;

            var selectedCase = interfaceOption.Cases[index];
            if (selectedCase.Option == null || selectedCase.Option.Count == 0) return;

            option.SubOptions ??= new List<MaaInterface.MaaInterfaceSelectOption>();

            foreach (var subOptionName in selectedCase.Option)
            {
                var existing = option.SubOptions.FirstOrDefault(o => o.Name == subOptionName);
                if (existing == null)
                {
                    existing = CreateDefaultSelectOption(subOptionName);
                    option.SubOptions.Add(existing);
                }
                AddSubOption(subOptionsContainer, existing, source);
            }
        }

        toggleSwitch.IsCheckedChanged += (_, _) =>
        {
            option.Index = toggleSwitch.IsChecked == true ? yesValue : noValue;
            UpdateSubOptions(option.Index ?? 0);
            saveConfigurationAction();
        };

        // Initialize sub-options
        UpdateSubOptions(option.Index ?? 0);

        // Label with Icon
        var labelPanel = CreateLabelPanel(option.DisplayName, option.Name, interfaceOption.Description, interfaceOption.Document);
        var icon = CreateIcon(interfaceOption);
        icon.Margin = new Thickness(10, 0, 6, 0);
        labelPanel.Children.Insert(0, icon);
        if (IsDailyPresetOption(option.Name))
            AppendDailyPresetGearIcon(labelPanel, option, source);
        else if (HasSubOptions(interfaceOption))
            AppendGearIcon(labelPanel, option, interfaceOption, source);

        Grid.SetColumn(labelPanel, 0);
        Grid.SetColumn(toggleSwitch, 2);
        
        grid.Children.Add(labelPanel);
        grid.Children.Add(toggleSwitch);
        wrapper.Children.Add(grid);

        // Enhanced Sub-option visualization
        var subOptionsBorder = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Background = Brushes.Transparent,
            // Improved margin/padding for better elegance
            Margin = new Thickness(24, 0, 0, 4), 
            Padding = new Thickness(8, 0, 0, 0),
            Child = subOptionsContainer
        };
        subOptionsBorder.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("SukiPrimaryColor"));
        subOptionsBorder.Bind(Visual.IsVisibleProperty,new Binding("Children.Count")
        {
            Source = subOptionsContainer,
            Converter = new FuncValueConverter<int, bool>(count => count > 0)
        });
        
        wrapper.Children.Add(subOptionsBorder);

        return wrapper;
    }

    private Control CreateComboBoxControl(
        MaaInterface.MaaInterfaceSelectOption option, 
        MaaInterface.MaaInterfaceOption interfaceOption,
        DragItemViewModel source)
    {
        var wrapper = new StackPanel();
        var grid = CreateBaseGrid();

        interfaceOption.Cases?.ForEach(c => c.InitializeDisplayName());
        interfaceOption.InitializeIcon();

        var comboBox = new ComboBox
        {
            MinWidth = 120,
            Classes = { "LimitWidth" },
            Margin = new Thickness(0, 2, 0, 2),
            ItemsSource = interfaceOption.Cases,
            SelectedIndex = option.Index ?? 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        
        BindIdleEnabled(comboBox);
        SetupComboBoxTemplate(comboBox);

        // Sub-options container
        var subOptionsContainer = new StackPanel();

        void UpdateSubOptions(int index)
        {
            subOptionsContainer.Children.Clear();
            if (HasSubOptions(interfaceOption)) return; // 有齿轮入口，子选项只在子页面显示
            if (interfaceOption.Cases == null || index < 0 || index >= interfaceOption.Cases.Count) return;

            var selectedCase = interfaceOption.Cases[index];
            if (selectedCase.Option == null || selectedCase.Option.Count == 0) return;

            option.SubOptions ??= new List<MaaInterface.MaaInterfaceSelectOption>();

            foreach (var subOptionName in selectedCase.Option)
            {
                var existing = option.SubOptions.FirstOrDefault(o => o.Name == subOptionName);
                if (existing == null)
                {
                    existing = CreateDefaultSelectOption(subOptionName);
                    option.SubOptions.Add(existing);
                }
                AddSubOption(subOptionsContainer, existing, source);
            }
        }

        comboBox.SelectionChanged += (_, _) =>
        {
            var selectedCase = comboBox.SelectedItem as MaaInterface.MaaInterfaceOptionCase;
            var resolvedIndex = selectedCase != null && interfaceOption.Cases != null
                ? interfaceOption.Cases.FindIndex(c => c.Name == selectedCase.Name)
                : comboBox.SelectedIndex;

            option.Index = resolvedIndex;
            UpdateSubOptions(resolvedIndex);
            saveConfigurationAction();
        };
        
        UpdateSubOptions(option.Index ?? 0);
        ComboBoxExtensions.SetDisableNavigationOnLostFocus(comboBox, true);
        ComboBoxExtensions.SetCanSearch(comboBox, interfaceOption.IsSearchable);
        ComboBoxExtensions.SetSearchMemberPath(comboBox, "DisplayName");
        comboBox.Bind(ComboBoxExtensions.SearchWatermarkProperty, new I18nBinding(LangKeys.Search));
        // Header
        var labelPanel = CreateLabelPanel(option.DisplayName, option.Name, interfaceOption.Description, interfaceOption.Document);
        var icon = CreateIcon(interfaceOption);
        icon.Margin = new Thickness(10, 0, 6, 0); // Margin adjusted
        labelPanel.Children.Insert(0, icon);
        if (HasSubOptions(interfaceOption))
            AppendGearIcon(labelPanel, option, interfaceOption, source);

        Grid.SetColumn(labelPanel, 0);
        Grid.SetColumn(comboBox, 1);
        
        AddResponsiveBehavior(grid, labelPanel, comboBox);
        
        grid.Children.Add(labelPanel);
        grid.Children.Add(comboBox);

        wrapper.Children.Add(grid);
        
        // Sub-options border
        var subOptionsBorder = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            // Improved margin/padding
            Margin = new Thickness(24, 0, 0, 4),
            Padding = new Thickness(8, 0, 0, 0),
            Child = subOptionsContainer
        };
        subOptionsBorder.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("SukiPrimaryColor"));
        subOptionsBorder.Bind(Visual.IsVisibleProperty, new Binding("Children.Count")
        {
            Source = subOptionsContainer,
            Converter = new FuncValueConverter<int, bool>(count => count > 0)
        });

        wrapper.Children.Add(subOptionsBorder);

        return wrapper;
    }
    
    // ... Methods for Advanced Options, Helper methods below ...

    private void AddAdvancedOption(Panel panel, MaaInterface.MaaInterfaceSelectAdvanced option)
    {
        if (MaaProcessor.Interface?.Advanced?.TryGetValue(option.Name, out var interfaceOption) != true) return;

        // Iterate fields
        for (int i = 0; interfaceOption.Field != null && i < interfaceOption.Field.Count; i++)
        {
            var field = interfaceOption.Field[i];
            var type = i < (interfaceOption.Type?.Count ?? 0) ? (interfaceOption.Type?[i] ?? "string") : (interfaceOption.Type?.Count > 0 ? interfaceOption.Type[0] : "string");

            // Resolve default value
            string defaultValue = string.Empty;
            if (option.Data.TryGetValue(field, out var value))
            {
                defaultValue = value;
            }
            else if (interfaceOption.Default != null && interfaceOption.Default.Count > i)
            {
                var defaultToken = interfaceOption.Default[i];
                defaultValue = defaultToken is JArray arr ? (arr.Count > 0 ? arr[0].ToString() : "") : defaultToken.ToString();
            }

            var grid = CreateBaseGrid();

            // AutoCompleteBox
            var autoCompleteBox = new AutoCompleteBox
            {
                MinWidth = 120,
                Margin = new Thickness(0, 2, 0, 2),
                Text = defaultValue,
                IsTextCompletionEnabled = true,
                FilterMode = AutoCompleteFilterMode.Custom,
                ItemFilter = (search, item) => 
                {
                    if (string.IsNullOrEmpty(search)) return true;
                    return item?.ToString()?.Contains(search, StringComparison.InvariantCultureIgnoreCase) ?? false;
                }
            };
            
            BindIdleEnabled(autoCompleteBox);
            
            // Completion Items
            if (interfaceOption.Default != null && interfaceOption.Default.Count > i)
            {
                var defaultToken = interfaceOption.Default[i];
                if (defaultToken is JArray arr)
                    autoCompleteBox.ItemsSource = arr.Select(item => item.ToString()).ToList();
                else
                    autoCompleteBox.ItemsSource = new List<string> { defaultToken.ToString(), string.Empty };
                
                if (autoCompleteBox.ItemsSource is System.Collections.IEnumerable items && items.Cast<object>().Any())
                      Interaction.GetBehaviors(autoCompleteBox).Add(new AutoCompleteBehavior());
            }

            // Events
            autoCompleteBox.TextChanged += (_, _) => HandleAdvancedInputChange(autoCompleteBox, option, field, type, interfaceOption);
            autoCompleteBox.SelectionChanged += (_, _) => 
            {
                if (autoCompleteBox.SelectedItem is string selectedText)
                {
                    autoCompleteBox.Text = selectedText;
                    // Change event will be fired by setting Text
                }
            };

            // Initial manual trigger if needed? No, Text is set.

            // Label
            var labelPanel = CreateLabelPanel(field, null, interfaceOption.Description, interfaceOption.Document, isResourceBinding: true);
            labelPanel.Margin = new Thickness(10, 0, 0, 0);

            Grid.SetColumn(labelPanel, 0);
            Grid.SetColumn(autoCompleteBox, 1);
            
            AddResponsiveBehavior(grid, labelPanel, autoCompleteBox);
            
            grid.Children.Add(labelPanel);
            grid.Children.Add(autoCompleteBox);
            panel.Children.Add(grid);
        }
    }

    private void AddSubOption(StackPanel container, MaaInterface.MaaInterfaceSelectOption subOption, DragItemViewModel source)
    {
         if (MaaProcessor.Interface?.Option?.TryGetValue(subOption.Name ?? string.Empty, out var subInterfaceOption) != true) return;
         
         Control control;
         if (subInterfaceOption.IsInput)
            control = CreateInputControl(subOption, subInterfaceOption);
         else if (subInterfaceOption.IsCheckbox)
            control = CreateCheckboxControl(subOption, subInterfaceOption, source);
         else if ((subInterfaceOption.IsSwitch && subInterfaceOption.Cases.ShouldSwitchButton(out var yes, out var no)) ||
                  subInterfaceOption.Cases.ShouldSwitchButton(out yes, out no))
            control = CreateToggleControl(subOption, yes, no, subInterfaceOption, source);
         else
            control = CreateComboBoxControl(subOption, subInterfaceOption, source);

         container.Children.Add(control);
    }

    // Helper Creation Methods

    private Grid CreateBaseGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) }
            },
            Margin = new Thickness(10, 3, 10, 3)
        };
        return grid;
    }

    private Grid CreateSpecialTaskGrid(bool isFirstRow = false)
    {
        var grid = CreateBaseGrid();
        grid.Margin = isFirstRow ? new Thickness(10, 6, 10, 6) : new Thickness(10, 3, 10, 3);
        return grid;
    }

    private StackPanel CreateLabelPanel(string displayName, string? name, string? description, List<string>? document = null, bool isResourceBinding = false, bool useI18n = false)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            MinWidth = 180,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        
        var textBlock = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        
        if (useI18n)
            textBlock.Bind(TextBlock.TextProperty, new I18nBinding(displayName));
        else if (isResourceBinding)
            textBlock.Bind(TextBlock.TextProperty, new ResourceBinding(displayName));
        else
            textBlock.Bind(TextBlock.TextProperty, new ResourceBindingWithFallback(displayName, name));

        textBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiLowText"));
        stackPanel.Children.Add(textBlock);
        
        var tooltipText = GetTooltipText(description, document);
        if (!string.IsNullOrWhiteSpace(tooltipText))
        {
            var docBlock = new TooltipBlock();
            docBlock.Bind(TooltipBlock.TooltipTextProperty, new ResourceBinding(tooltipText));
            docBlock.Margin = new Thickness(4, 0, 0, 0); // Spacing
            stackPanel.Children.Add(docBlock);
        }
        
        return stackPanel;
    }

    private static TextBlock CreateLabelText(string key)
    {
        var textBlock = new TextBlock
        {
            FontSize = 14,
            MinWidth = 180,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        textBlock.Bind(TextBlock.TextProperty, new I18nBinding(key));
        textBlock.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiLowText"));
        return textBlock;
    }

    private DisplayIcon CreateIcon(MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var iconDisplay = new DisplayIcon
        {
            IconSize = 20,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconDisplay.Bind(DisplayIcon.IconSourceProperty, new Binding(nameof(MaaInterface.MaaInterfaceOption.ResolvedIcon)) { Source = interfaceOption });
        iconDisplay.Bind(Visual.IsVisibleProperty, new Binding(nameof(MaaInterface.MaaInterfaceOption.HasIcon)) { Source = interfaceOption });
        return iconDisplay;
    }

    private Control CreateOptionHeader(MaaInterface.MaaInterfaceOption interfaceOption)
    {
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 4)
        };

        var icon = CreateIcon(interfaceOption);
        icon.Margin = new Thickness(0, 0, 6, 0);
        headerPanel.Children.Add(icon);

        var headerText = new TextBlock
        {
            FontSize = 14,
        };
        headerText.Bind(TextBlock.TextProperty, new ResourceBindingWithFallback(interfaceOption.DisplayName, interfaceOption.Name));
        headerText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiLowText"));
        headerPanel.Children.Add(headerText);

        // 添加 TooltipBlock 显示 Option 级别的 Description
        var tooltipText = GetTooltipText(interfaceOption.Description, interfaceOption.Document);
        if (!string.IsNullOrWhiteSpace(tooltipText))
        {
            var docBlock = new TooltipBlock();
            docBlock.Bind(TooltipBlock.TooltipTextProperty, new ResourceBinding(tooltipText));
            docBlock.Margin = new Thickness(4, 0, 0, 0);
            headerPanel.Children.Add(docBlock);
        }

        return headerPanel;
    }

    private void BindIdleEnabled(Control control)
    {
        control.Bind(Control.IsEnabledProperty, new Binding("Idle") { Source = Instances.RootViewModel });
    }

    private void AddResponsiveBehavior(Grid grid, Control label, Control input)
    {
        // 文字与输入框始终同一行（两列布局），不做窄屏换行
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) });

        Grid.SetRow(label, 0);
        Grid.SetRow(input, 0);
        Grid.SetColumn(label, 0);
        Grid.SetColumn(input, 1);
    }

    // Logic Helpers

    private void HandleStringInputChange(TextBox textBox, MaaInterface.MaaInterfaceOptionInput input, MaaInterface.MaaInterfaceSelectOption option, MaaInterface.MaaInterfaceOption interfaceOption, bool silent = false)
    {
        var text = textBox.Text ?? string.Empty;
        var pipelineType = input.PipelineType?.ToLower() ?? "string";

        if (pipelineType == "int" && !IsValidIntegerInput(text))
        {
            var oldIdx = textBox.CaretIndex;
            textBox.Text = FilterToInteger(text);
            if (oldIdx <= textBox.Text.Length) textBox.CaretIndex = oldIdx;
            return;
        }

        if (!string.IsNullOrEmpty(input.Verify))
        {
             // Regex validation... logic as before
        }

        // 验证输入并显示红框
        var validation = interfaceOption.ValidateInput(input.Name ?? string.Empty, text);
        if (validation.IsValid)
            textBox.Bind(TextBox.BorderBrushProperty, new DynamicResourceExtension("SukiControlBorderBrush"));
        else
            textBox.BorderBrush = Brushes.Red;

        if (!silent)
        {
            option.Data[input.Name] = text == "null" ? MaaInterface.MaaInterfaceOption.ExplicitNullMarker : text;
            UpdatePipeline(option, interfaceOption);
            saveConfigurationAction();
        }
    }
    
    private void HandleAdvancedInputChange(AutoCompleteBox box, MaaInterface.MaaInterfaceSelectAdvanced option, string field, string type, MaaInterfaceAdvancedOption interfaceOption)
    {
        if (type.ToLower() == "int" && !IsValidIntegerInput(box.Text))
                box.Text = FilterToInteger(box.Text);

        option.Data[field] = box.Text;
        option.PipelineOverride = interfaceOption.GenerateProcessedPipeline(option.Data);
        saveConfigurationAction();
    }
    
    private void UpdatePipeline(MaaInterface.MaaInterfaceSelectOption option, MaaInterface.MaaInterfaceOption interfaceOption)
    {
        if (interfaceOption.PipelineOverride != null && option.Data != null)
        {
             option.PipelineOverride = interfaceOption.GenerateProcessedPipeline(
                 option.Data.Where(kv => kv.Value != null).ToDictionary(kv => kv.Key, kv => kv.Value!));
        }
    }

    private static MaaInterface.MaaInterfaceSelectOption CreateDefaultSelectOption(string optionName)
    {
        var selectOption = new MaaInterface.MaaInterfaceSelectOption { Name = optionName, Index = 0 };
        if (MaaProcessor.Interface?.Option?.TryGetValue(optionName, out var interfaceOption) == true)
        {
            if (interfaceOption.IsInput && interfaceOption.Inputs != null)
            {
                selectOption.Data = new Dictionary<string, string?>();
                foreach(var input in interfaceOption.Inputs)
                    if (!string.IsNullOrEmpty(input.Name))
                        selectOption.Data[input.Name] = input.Default ?? string.Empty;
            }
            else if (interfaceOption.IsCheckbox)
            {
                // 任务 9：checkbox 类型初始化 SelectedCases from DefaultCases
                selectOption.SelectedCases = new List<string>(interfaceOption.DefaultCases ?? new List<string>());
            }
            else if (!string.IsNullOrEmpty(interfaceOption.DefaultCase) && interfaceOption.Cases != null)
            {
                var idx = interfaceOption.Cases.FindIndex(c => c.Name == interfaceOption.DefaultCase);
                if (idx >= 0) selectOption.Index = idx;
            }
        }
        return selectOption;
    }

    private static string? GetTooltipText(string? description, List<string>? document)
    {
         if (!string.IsNullOrWhiteSpace(description))
         {
             var result = description.ResolveContentAsync(transform: false).GetAwaiter().GetResult();
             // 如果结果与输入相同（未被解析），尝试通过 resx i18n 系统解析（支持特殊任务描述等）
             if (result == description)
             {
                 var localized = description.ToLocalization();
                 if (localized != description)
                     return localized;
             }
             return result;
         }
             
         if (document is { Count: > 0 })
         {
             try { return LanguageHelper.GetLocalizedString(Regex.Unescape(string.Join("\\n", document))); }
             catch { return string.Join("\n", document); }
         }
         return null;
    }
    
    // Utils
    private bool IsValidIntegerInput(string text)
    {
        if (string.IsNullOrEmpty(text) || text == "-") return true;
        if (text.StartsWith("-") && (text.Length == 1 || (!char.IsDigit(text[1])))) return false;
        return text.All(c => c == '-' || char.IsDigit(c)) && text.Count(c => c == '-') <= 1 && text.LastIndexOf('-') <= 0;
    }
    private string FilterToInteger(string text)
    {
         var filtered = new string(text.Where(c => c == '-' || char.IsDigit(c)).ToArray());
         if (filtered.Contains('-') && (filtered[0] != '-' || filtered.Count(c => c == '-') > 1))
             filtered = filtered.Replace("-", "");
         if (string.IsNullOrEmpty(filtered) || filtered == "-") return filtered;
         return filtered.Length > 1 && filtered[0] == '0' ? filtered.TrimStart('0') : filtered; 
    }
    
    private void SetupComboBoxTemplate(ComboBox comboBox)
    {
        // ... Copy ItemTemplate and SelectionBoxItemTemplate from original ...
        // Simplification for brevity in this step, but fully implemented in real code
        
        // IMPORTANT: Copying the complex DataTemplates for ComboBox items (Icons + MarqueeText/TextBlock + Tooltip)
        // I will implement a helper to create the DataTemplate to avoid duplicating code between ItemTemplate and SelectionBoxItemTemplate
        comboBox.ItemTemplate = new FuncDataTemplate<MaaInterface.MaaInterfaceOptionCase>((caseOption, _) => CreateComboBoxItemContent(caseOption, true));
        comboBox.SelectionBoxItemTemplate = new FuncDataTemplate<MaaInterface.MaaInterfaceOptionCase>((caseOption, _) => CreateComboBoxItemContent(caseOption, false));
    }
    
    private Control CreateComboBoxItemContent(MaaInterface.MaaInterfaceOptionCase? caseOption, bool isMarquee)
    {
         var grid = new Grid
         {
             ColumnDefinitions =
             {
                 new ColumnDefinition { Width = GridLength.Auto },
                 new ColumnDefinition { Width = GridLength.Star },
                 new ColumnDefinition { Width = GridLength.Auto }
             },
             HorizontalAlignment = HorizontalAlignment.Stretch
         };
         
         var iconDisplay = new DisplayIcon { IconSize = 20, Margin = new Thickness(0,0,6,0), VerticalAlignment = VerticalAlignment.Center };
         iconDisplay.Bind(DisplayIcon.IconSourceProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.ResolvedIcon)) { Source = caseOption });
         iconDisplay.Bind(Visual.IsVisibleProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.HasIcon)) { Source = caseOption });
         Grid.SetColumn(iconDisplay, 0);

         Control textControl;
         if (isMarquee)
         {
             var marquee = new MarqueeTextBlock { VerticalContentAlignment = VerticalAlignment.Center };
             marquee.Bind(MarqueeTextBlock.ForegroundProperty, new DynamicResourceExtension("SukiText"));
             marquee.Bind(MarqueeTextBlock.TextProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayName)) { Source = caseOption });
             marquee.Bind(ToolTip.TipProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayName)) { Source = caseOption });
             ToolTip.SetShowDelay(marquee, 100);
             textControl = marquee;
         }
         else
         {
             var tb = new TextBlock { TextTrimming = TextTrimming.WordEllipsis, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
             tb.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SukiText"));
             tb.Bind(TextBlock.TextProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayName)) { Source = caseOption });
             tb.Bind(ToolTip.TipProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayName)) { Source = caseOption });
             textControl = tb;
         }
         Grid.SetColumn(textControl, 1);
         
         // 始终创建 TooltipBlock，由绑定控制可见性，避免模板回收阶段 caseOption 为空时空引用
         var tooltipBlock = new TooltipBlock { Margin = new Thickness(4, 0, 0, 0) };
         tooltipBlock.Bind(TooltipBlock.TooltipTextProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.DisplayDescription)) { Source = caseOption });
         tooltipBlock.Bind(Visual.IsVisibleProperty, new Binding(nameof(MaaInterface.MaaInterfaceOptionCase.HasDescription)) { Source = caseOption });
         Grid.SetColumn(tooltipBlock, 2);
         grid.Children.Add(tooltipBlock);
         
         grid.Children.Add(iconDisplay);
         grid.Children.Add(textControl);
         
         return grid;
    }

    #region 特殊任务选项面板

    /// <summary>
    /// 获取特殊任务当前的 custom_action_param JObject
    /// </summary>
    private static JObject GetActionParam(DragItemViewModel dragItem)
    {
        var entry = dragItem.InterfaceItem?.Entry;
        if (string.IsNullOrEmpty(entry) || dragItem.InterfaceItem?.PipelineOverride == null)
            return new JObject();

        if (dragItem.InterfaceItem.PipelineOverride.TryGetValue(entry, out var node) && node is JObject obj)
        {
            var paramToken = obj["custom_action_param"];
            if (paramToken is JObject paramObj)
                return paramObj;
            // 兼容旧版序列化为字符串的情况
            if (paramToken is JValue { Type: JTokenType.String } strVal)
            {
                try { return JObject.Parse((string)strVal!); }
                catch { return new JObject(); }
            }
        }
        return AddTaskDialogViewModel.GetDefaultActionParam(entry);
    }

    /// <summary>
    /// 更新特殊任务的 custom_action_param
    /// </summary>
    private void UpdateActionParam(DragItemViewModel dragItem, JObject param)
    {
        var entry = dragItem.InterfaceItem?.Entry;
        if (string.IsNullOrEmpty(entry) || dragItem.InterfaceItem?.PipelineOverride == null) return;

        if (dragItem.InterfaceItem.PipelineOverride.TryGetValue(entry, out var node) && node is JObject obj)
        {
            obj["custom_action_param"] = param;
        }
        saveConfigurationAction();
    }

    /// <summary>
    /// 根据 Entry 分发到对应的特殊任务选项面板生成方法
    /// </summary>
    private void GenerateSpecialTaskPanelContent(StackPanel panel, DragItemViewModel dragItem)
    {
        var entry = dragItem.InterfaceItem?.Entry;
        if (string.IsNullOrEmpty(entry)) return;

        switch (entry)
        {
            case "CountdownAction":
                AddCountdownOptions(panel, dragItem);
                break;
            case "TimedWaitAction":
                AddTimedWaitOptions(panel, dragItem);
                break;
            case "SystemNotificationAction":
                AddSystemNotificationOptions(panel, dragItem);
                break;
            case "CustomProgramAction":
                AddCustomProgramOptions(panel, dragItem);
                break;
            case "KillProcessAction":
                AddKillProcessOptions(panel, dragItem);
                break;
            case "ComputerOperationAction":
                AddComputerOperationOptions(panel, dragItem);
                break;
            case "WebhookAction":
                AddWebhookOptions(panel, dragItem);
                break;
            case "FormationConfig":
                AddFormationOptions(panel, dragItem);
                break;
        }
    }

    /// <summary>
    /// 自定编队 - 开关与预设管理面板
    /// </summary>
    private void AddFormationOptions(StackPanel panel, DragItemViewModel dragItem)
    {
        var param = GetActionParam(dragItem);

        // 预设区（任务勾选即为启用，无需单独开关）
        var presetArea = new StackPanel { Margin = new Thickness(10, 2, 10, 4) };
        panel.Children.Add(presetArea);

        presetArea.Children.Add(new TextBlock
        {
            Text = "预设（勾选互斥，选择本次要编成的预设）",
            FontSize = 13,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 2, 0, 2),
        });

        var presetList = new StackPanel { Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };
        presetArea.Children.Add(presetList);

        RenderFormationPresets(presetList,
            () => param["preset_id"]?.Value<int>() ?? 0,
            id =>
            {
                param["preset_id"] = id;
                UpdateActionParam(dragItem, param);
            },
            dragItem);
    }

    /// <summary>
    /// 渲染预设行列表：互斥勾选 + 名称输入 + 齿轮（编辑）+ 下拉（删除/复制/粘贴）+ 底部加号
    /// getSelectedId/selectPreset 抽象 preset_id 的读写源：自定编队任务写入 custom_action_param，
    /// 一键日课写入任务选项的 Data 字典
    /// </summary>
    private void RenderFormationPresets(StackPanel presetList, Func<int> getSelectedId, Action<int> selectPreset, DragItemViewModel dragItem)
    {
        presetList.Children.Clear();

        var presets = LoadFormationPresets();
        var selectedId = getSelectedId();

        void Refresh() => RenderFormationPresets(presetList, getSelectedId, selectPreset, dragItem);

        foreach (var preset in presets)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // 互斥勾选（纯勾选框，不显示文字）
            var checkBox = new CheckBox
            {
                IsChecked = preset.Id == selectedId,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var capturedPreset = preset;
            checkBox.IsCheckedChanged += (_, _) =>
            {
                if (checkBox.IsChecked != true) return;
                selectPreset(capturedPreset.Id);
                Refresh();
            };
            row.Children.Add(checkBox);

            // 固定编号「预设N」，改名不影响
            row.Children.Add(new TextBlock
            {
                Text = $"预设{capturedPreset.Id}",
                FontSize = 13,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 48,
            });

            // 名称输入（失焦时保存）
            var nameBox = new TextBox
            {
                Text = preset.Name,
                Watermark = $"预设{capturedPreset.Id}",
                MinWidth = 160,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13,
            };
            nameBox.LostFocus += (_, _) =>
            {
                if (nameBox.Text?.Trim() == capturedPreset.Name) return;
                capturedPreset.Name = nameBox.Text?.Trim() ?? "";
                SaveFormationPresets(presets);
            };
            row.Children.Add(nameBox);

            // 齿轮：进入编辑界面
            var gearIcon = new FluentIcons.Avalonia.Fluent.FluentIcon
            {
                Icon = FluentIcons.Common.Icon.Settings,
                IconSize = FluentIcons.Common.IconSize.Size16,
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
            };
            gearIcon.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                OpenFormationEditor(capturedPreset, saved =>
                {
                    if (saved != null) SaveFormationPresets(presets);
                    viewModel.IsSubPageOpen = false;
                    Refresh();
                });
            };
            row.Children.Add(gearIcon);

            // 下拉菜单：删除 / 复制 / 粘贴
            var moreButton = new Button
            {
                Content = "▾",
                Padding = new Thickness(6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
            };
            var menu = new ContextMenu();
            var deleteItem = new MenuItem { Header = "删除" };
            deleteItem.Click += (_, _) =>
            {
                presets.Remove(capturedPreset);
                if (getSelectedId() == capturedPreset.Id)
                {
                    selectPreset(0);
                }
                RemapPresetIdsAfterDelete(capturedPreset.Id, presets);
                SaveFormationPresets(presets);
                RemapPresetReferences(capturedPreset.Id);
                Refresh();
            };
            var copyItem = new MenuItem { Header = "复制" };
            copyItem.Click += (_, _) => _formationClipboard = ClonePreset(capturedPreset);
            var pasteItem = new MenuItem { Header = "粘贴", IsEnabled = _formationClipboard != null };
            pasteItem.Click += (_, _) =>
            {
                if (_formationClipboard == null) return;
                capturedPreset.Team = _formationClipboard.Team;
                capturedPreset.Slots = CloneSlots(_formationClipboard);
                SaveFormationPresets(presets);
                Refresh();
            };
            menu.Items.Add(deleteItem);
            menu.Items.Add(copyItem);
            menu.Items.Add(pasteItem);
            moreButton.Click += (_, _) => menu.Open(moreButton);
            row.Children.Add(moreButton);

            presetList.Children.Add(row);
        }

        // 加号：新增预设（新增后直接进入编辑）
        var addButton = new Button
        {
            Content = "＋ 新增预设",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 6, 0, 0),
        };
        addButton.Click += (_, _) =>
        {
            var presetsNow = LoadFormationPresets();
            var newId = presetsNow.Count == 0 ? 1 : presetsNow.Max(p => p.Id) + 1;
            var preset = new FormationPreset { Id = newId, Name = $"预设{newId}", Team = 1 };
            preset.EnsureSlots();
            presetsNow.Add(preset);
            SaveFormationPresets(presetsNow);
            Refresh();
            OpenFormationEditor(preset, saved =>
            {
                if (saved != null) SaveFormationPresets(presetsNow);
                viewModel.IsSubPageOpen = false;
                Refresh();
            });
        };
        presetList.Children.Add(addButton);
    }

    /// <summary>读取全部编队预设（含缺位补齐）</summary>
    private static List<FormationPreset> LoadFormationPresets()
    {
        var presets = ConfigurationManager.CurrentInstance.GetValue<List<FormationPreset>>(ConfigurationKeys.FormationPresets, []);
        presets ??= [];
        foreach (var preset in presets)
            preset.EnsureSlots();
        return presets;
    }

    /// <summary>保存编队预设列表</summary>
    private static void SaveFormationPresets(List<FormationPreset> presets)
    {
        ConfigurationManager.CurrentInstance.SetValue(ConfigurationKeys.FormationPresets, presets);
    }

    /// <summary>
    /// 删除预设后重编号：Id 大于被删 Id 的预设顺移 -1，保持编号连续；
    /// 默认名（预设N）跟随顺移，自定义名保持不变
    /// </summary>
    private static void RemapPresetIdsAfterDelete(int removedId, List<FormationPreset> presets)
    {
        foreach (var preset in presets)
        {
            if (preset.Id <= removedId) continue;
            if (preset.Name == $"预设{preset.Id}")
                preset.Name = $"预设{preset.Id - 1}";
            preset.Id -= 1;
        }
    }

    /// <summary>
    /// 同步删除预设后的 preset_id 引用：被删 Id 置 0，大于被删 Id 的减 1。
    /// 覆盖自定编队任务的 custom_action_param 与一键日课的选项 Data，改内存后由保存回调落盘
    /// </summary>
    private void RemapPresetReferences(int removedId)
    {
        foreach (var item in viewModel.TaskItemViewModels)
        {
            var interfaceItem = item.InterfaceItem;
            if (interfaceItem == null) continue;

            // 自定编队任务：PipelineOverride[FormationConfig].custom_action_param.preset_id
            if (interfaceItem.Entry == "FormationConfig"
                && interfaceItem.PipelineOverride?.TryGetValue("FormationConfig", out var fcToken) == true
                && fcToken is JObject fcObj
                && fcObj["custom_action_param"] is JObject fcParam
                && fcParam["preset_id"] is JValue fcPreset)
            {
                var id = fcPreset.Value<int>();
                fcParam["preset_id"] = id == removedId ? 0 : (id > removedId ? id - 1 : id);
            }

            // 一键日课任务：选项 data.preset_id
            var presetOption = interfaceItem.Option?.FirstOrDefault(o => o.Name == "D_启用预设部队");
            if (presetOption?.Data != null
                && presetOption.Data.TryGetValue("preset_id", out var raw)
                && int.TryParse(raw, out var optionId))
            {
                presetOption.Data["preset_id"] = (optionId == removedId ? 0 : (optionId > removedId ? optionId - 1 : optionId)).ToString();
            }
        }
        saveConfigurationAction();
    }

    /// <summary>预设显示名：空名称时回退为「预设N」</summary>
    private static string DisplayNameOf(FormationPreset preset)
        => string.IsNullOrWhiteSpace(preset.Name) ? $"预设{preset.Id}" : preset.Name;

    /// <summary>深拷贝预设（复制用）</summary>
    private static FormationPreset ClonePreset(FormationPreset source)
    {
        return new FormationPreset
        {
            Id = source.Id,
            Name = source.Name,
            Team = source.Team,
            Slots = CloneSlots(source),
        };
    }

    /// <summary>深拷贝预设槽位列表（粘贴用）</summary>
    private static List<FormationSlot> CloneSlots(FormationPreset source)
    {
        return source.Slots.Select(s => new FormationSlot
        {
            Sword = s.Sword,
            Equip = s.Equip,
            Horse = s.Horse,
        }).ToList();
    }

    /// <summary>打开预设编辑覆盖层（SubPage），saved 非 null 表示已保存</summary>
    private void OpenFormationEditor(FormationPreset preset, Action<FormationPreset?>? onDone)
    {
        var editorVm = new FormationEditorViewModel(preset, onDone);
        viewModel.SubPageTitle = "编辑预设";
        viewModel.SubPageContent = new FormationEditorView { DataContext = editorVm };
        viewModel.IsSubPageOpen = true;
    }

    /// <summary>
    /// 倒计时 - seconds (NumericUpDown)
    /// </summary>
    private void AddCountdownOptions(StackPanel panel, DragItemViewModel dragItem)
    {
        var param = GetActionParam(dragItem);
        var grid = CreateSpecialTaskGrid(isFirstRow: true);

        var label = CreateLabelPanel(LangKeys.SpecialTask_CountdownSeconds, null, null, useI18n: true);
        label.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var numericUpDown = new NumericUpDown
        {
            Value = (int?)param["seconds"] ?? 60,
            Minimum = 1,
            Maximum = 86400,
            Increment = 1,
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Classes = { "TaskOptionLikeCombo" },
        };
        BindIdleEnabled(numericUpDown);
        numericUpDown.ValueChanged += (_, _) =>
        {
            param["seconds"] = Convert.ToInt32(numericUpDown.Value);
            UpdateActionParam(dragItem, param);
        };

        Grid.SetColumn(numericUpDown, 1);
        AddResponsiveBehavior(grid, label, numericUpDown);
        grid.Children.Add(numericUpDown);
        panel.Children.Add(grid);
    }

    /// <summary>
    /// 定时等待 - 等待到指定时间 (hour:minute)，使用 TimePicker 控件
    /// </summary>
    private void AddTimedWaitOptions(StackPanel panel, DragItemViewModel dragItem)
    {
        var param = GetActionParam(dragItem);
        var grid = CreateSpecialTaskGrid(isFirstRow: true);

        var label = CreateLabelPanel(LangKeys.SpecialTask_WaitUntilTime, null, null, useI18n: true);
        label.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var hour = (int?)param["hour"] ?? 0;
        var minute = (int?)param["minute"] ?? 0;

        var timePicker = new TimePicker
        {
            ClockIdentifier = "24HourClock",
            SelectedTime = new TimeSpan(hour, minute, 0),
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Classes = { "TaskOptionLikeCombo" },
        };
        BindIdleEnabled(timePicker);

        timePicker.SelectedTimeChanged += (_, _) =>
        {
            var time = timePicker.SelectedTime ?? TimeSpan.Zero;
            param["hour"] = time.Hours;
            param["minute"] = time.Minutes;
            UpdateActionParam(dragItem, param);
        };

        Grid.SetColumn(timePicker, 1);
        AddResponsiveBehavior(grid, label, timePicker);
        grid.Children.Add(timePicker);
        panel.Children.Add(grid);
    }

    /// <summary>
    /// 系统通知 - title + message (TextBox)
    /// </summary>
    private void AddSystemNotificationOptions(StackPanel panel, DragItemViewModel dragItem)
    {
        var param = GetActionParam(dragItem);

        // 标题
        var grid1 = CreateSpecialTaskGrid(isFirstRow: true);
        var label1 = CreateLabelPanel(LangKeys.SpecialTask_NotificationTitle, null, null, useI18n: true);
        label1.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label1, 0);
        grid1.Children.Add(label1);

        var titleBox = new TextBox
        {
            Text = (string?)param["title"] ?? "MFAAvalonia",
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindIdleEnabled(titleBox);
        titleBox.TextChanged += (_, _) =>
        {
            param["title"] = titleBox.Text ?? "";
            UpdateActionParam(dragItem, param);
        };
        Grid.SetColumn(titleBox, 1);
        AddResponsiveBehavior(grid1, label1, titleBox);
        grid1.Children.Add(titleBox);
        panel.Children.Add(grid1);

        // 内容
        var grid2 = CreateSpecialTaskGrid();
        var label2 = CreateLabelPanel(LangKeys.SpecialTask_NotificationContent, null, null, useI18n: true);
        label2.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label2, 0);
        grid2.Children.Add(label2);

        var messageBox = new TextBox
        {
            Text = (string?)param["message"] ?? "",
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 100,
        };
        BindIdleEnabled(messageBox);
        messageBox.TextChanged += (_, _) =>
        {
            param["message"] = messageBox.Text ?? "";
            UpdateActionParam(dragItem, param);
        };
        Grid.SetColumn(messageBox, 1);
        AddResponsiveBehavior(grid2, label2, messageBox);
        grid2.Children.Add(messageBox);
        panel.Children.Add(grid2);
    }

    /// <summary>
    /// 自定义程序 - program (TextBox+拖拽+文件选择), arguments (TextBox), wait_for_exit (ToggleSwitch)
    /// </summary>
    private void AddCustomProgramOptions(StackPanel panel, DragItemViewModel dragItem)
    {
        var param = GetActionParam(dragItem);

        // 程序路径
        var grid1 = CreateSpecialTaskGrid(isFirstRow: true);
        var label1 = CreateLabelPanel(LangKeys.SpecialTask_ProgramPath, null, null, useI18n: true);
        label1.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label1, 0);
        grid1.Children.Add(label1);

        var programBox = new TextBox
        {
            Text = (string?)param["program"] ?? "",
            Margin = new Thickness(0, 2, 0, 2),
            MinWidth = 120,
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindIdleEnabled(programBox);

        // InnerRightContent: 文件选择按钮（参考 StartSettings 游戏路径样式）
        var browseBtn = new Button
        {
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Height = 20,
            Width = 30,
            Padding = new Thickness(-4, 0, 0, 0),
            Content = new FluentIcons.Avalonia.Fluent.FluentIcon()
            {
                Icon = FluentIcons.Common.Icon.FolderArrowLeft,
                IconSize = FluentIcons.Common.IconSize.Size16,
            },
        };
        browseBtn.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(panel);
            if (topLevel?.StorageProvider == null) return;
            var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LangKeys.SpecialTask_SelectProgram.ToLocalization(),
                AllowMultiple = false,
            });
            if (result.Count > 0)
            {
                programBox.Text = result[0].Path.LocalPath;
            }
        };
        programBox.InnerRightContent = browseBtn;

        // 支持拖拽
        DragDrop.SetAllowDrop(programBox, true);
        programBox.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
#pragma warning disable CS0618
            if (e.Data.GetFiles() is { } files)
#pragma warning restore CS0618
            {
                var file = files.FirstOrDefault();
                if (file != null)
                {
                    programBox.Text = file.Path.LocalPath;
                }
            }
        });
        programBox.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Copy;
        });

        programBox.TextChanged += (_, _) =>
        {
            param["program"] = programBox.Text ?? "";
            UpdateActionParam(dragItem, param);
        };

        Grid.SetColumn(programBox, 1);
        AddResponsiveBehavior(grid1, label1, programBox);
        grid1.Children.Add(programBox);
        panel.Children.Add(grid1);

        // 附加参数
        var grid2 = CreateSpecialTaskGrid();
        var label2 = CreateLabelPanel(LangKeys.SpecialTask_Arguments, null, null, useI18n: true);
        label2.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label2, 0);
        grid2.Children.Add(label2);

        var argsBox = new TextBox
        {
            Text = (string?)param["arguments"] ?? "",
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindIdleEnabled(argsBox);
        argsBox.TextChanged += (_, _) =>
        {
            param["arguments"] = argsBox.Text ?? "";
            UpdateActionParam(dragItem, param);
        };
        Grid.SetColumn(argsBox, 1);
        AddResponsiveBehavior(grid2, label2, argsBox);
        grid2.Children.Add(argsBox);
        panel.Children.Add(grid2);

        // 等待退出
        var grid3 = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Margin = new Thickness(10, 3, 10, 3),
        };
        var label3 = CreateLabelPanel(LangKeys.SpecialTask_WaitForExit, null, null, useI18n: true);
        label3.Margin = new Thickness(10, 0, 5, 0);
        Grid.SetColumn(label3, 0);
        grid3.Children.Add(label3);

        var waitToggle = new ToggleSwitch
        {
            IsChecked = (bool?)param["wait_for_exit"] ?? false,
            Classes = { "Switch" },
            MaxHeight = 60,
            MaxWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindIdleEnabled(waitToggle);
        waitToggle.IsCheckedChanged += (_, _) =>
        {
            param["wait_for_exit"] = waitToggle.IsChecked == true;
            UpdateActionParam(dragItem, param);
        };
        Grid.SetColumn(waitToggle, 2);
        grid3.Children.Add(waitToggle);
        panel.Children.Add(grid3);
    }

    /// <summary>
    /// 结束进程 - kill_self_process (ToggleSwitch), process_name (TextBox)
    /// </summary>
    private void AddKillProcessOptions(StackPanel panel, DragItemViewModel dragItem)
    {
        var param = GetActionParam(dragItem);
        var processName = (string?)param["process_name"] ?? string.Empty;
        var killSelfProcess = (bool?)param["kill_self_process"] ?? string.IsNullOrWhiteSpace(processName);

        var wrapper = new StackPanel();
        var grid = CreateSpecialTaskGrid(isFirstRow: true);

        var labelPanel = CreateLabelPanel(LangKeys.SpecialTask_KillSelfProcess, null, LangKeys.SpecialTask_KillSelfProcessDesc, useI18n: true);
        Grid.SetColumn(labelPanel, 0);
        grid.Children.Add(labelPanel);

        var toggleSwitch = new ToggleSwitch
        {
            IsChecked = killSelfProcess,
            Classes = { "Switch" },
            MaxHeight = 60,
            MaxWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindIdleEnabled(toggleSwitch);
        Grid.SetColumn(toggleSwitch, 2);
        grid.Children.Add(toggleSwitch);
        wrapper.Children.Add(grid);

        var subOptionsContainer = new StackPanel();

        void UpdateSubOptions(bool isKillSelf)
        {
            subOptionsContainer.Children.Clear();
            if (isKillSelf)
                return;

            var processGrid = CreateSpecialTaskGrid();
            processGrid.Margin = new Thickness(0, 0, 0, 0);

            var processLabel = CreateLabelPanel(LangKeys.SpecialTask_ProcessName, null, null, useI18n: true);
            Grid.SetColumn(processLabel, 0);
            processGrid.Children.Add(processLabel);

            var textBox = new TextBox
            {
                Text = (string?)param["process_name"] ?? string.Empty,
                MinWidth = 120,
                Margin = new Thickness(0, 2, 0, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Watermark = LangKeys.SpecialTask_ProcessNamePlaceholder.ToLocalization(),
            };
            BindIdleEnabled(textBox);
            textBox.TextChanged += (_, _) =>
            {
                param["process_name"] = textBox.Text ?? string.Empty;
                UpdateActionParam(dragItem, param);
            };

            Grid.SetColumn(textBox, 1);
            AddResponsiveBehavior(processGrid, processLabel, textBox);
            processGrid.Children.Add(textBox);
            subOptionsContainer.Children.Add(processGrid);
        }

        toggleSwitch.IsCheckedChanged += (_, _) =>
        {
            var isKillSelf = toggleSwitch.IsChecked == true;
            param["kill_self_process"] = isKillSelf;
            UpdateActionParam(dragItem, param);
            UpdateSubOptions(isKillSelf);
        };

        UpdateSubOptions(killSelfProcess);

        var subOptionsBorder = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            Background = Brushes.Transparent,
            Margin = new Thickness(24, 0, 0, 4),
            Padding = new Thickness(8, 0, 0, 0),
            Child = subOptionsContainer
        };
        subOptionsBorder.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("SukiPrimaryColor"));
        subOptionsBorder.Bind(Visual.IsVisibleProperty, new Binding("Children.Count")
        {
            Source = subOptionsContainer,
            Converter = new FuncValueConverter<int, bool>(count => count > 0)
        });

        wrapper.Children.Add(subOptionsBorder);
        panel.Children.Add(wrapper);
    }

    /// <summary>
    /// 电脑操作 - operation (ComboBox: shutdown/restart/sleep/hibernate)
    /// </summary>
    private void AddComputerOperationOptions(StackPanel panel, DragItemViewModel dragItem)
    {
        var param = GetActionParam(dragItem);
        var grid = CreateSpecialTaskGrid(isFirstRow: true);

        var label = CreateLabelPanel(LangKeys.SpecialTask_OperationType, null, null, useI18n: true);
        label.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        var operations = new List<string> { "shutdown", "restart", "sleep", "hibernate" };
        var displayNames = new List<string>
        {
            LangKeys.SpecialTask_Shutdown.ToLocalization(),
            LangKeys.SpecialTask_Restart.ToLocalization(),
            LangKeys.SpecialTask_Sleep.ToLocalization(),
            LangKeys.SpecialTask_Hibernate.ToLocalization(),
        };

        var comboBox = new ComboBox
        {
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            ItemsSource = displayNames,
            SelectedIndex = Math.Max(0, operations.IndexOf((string?)param["operation"] ?? "shutdown")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        BindIdleEnabled(comboBox);
        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < operations.Count)
            {
                param["operation"] = operations[comboBox.SelectedIndex];
                UpdateActionParam(dragItem, param);
            }
        };

        Grid.SetColumn(comboBox, 1);
        AddResponsiveBehavior(grid, label, comboBox);
        grid.Children.Add(comboBox);
        panel.Children.Add(grid);
    }

    /// <summary>
    /// Webhook - url, method (GET/POST), body, content_type
    /// </summary>
    private void AddWebhookOptions(StackPanel panel, DragItemViewModel dragItem)
    {
        var param = GetActionParam(dragItem);

        // URL
        var grid1 = CreateSpecialTaskGrid(isFirstRow: true);
        var label1 = CreateLabelPanel("URL", null, null);
        label1.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label1, 0);
        grid1.Children.Add(label1);

        var urlBox = new TextBox
        {
            Text = (string?)param["url"] ?? "",
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Watermark = "https://example.com/webhook",
        };
        BindIdleEnabled(urlBox);
        urlBox.TextChanged += (_, _) =>
        {
            param["url"] = urlBox.Text ?? "";
            UpdateActionParam(dragItem, param);
        };
        Grid.SetColumn(urlBox, 1);
        AddResponsiveBehavior(grid1, label1, urlBox);
        grid1.Children.Add(urlBox);
        panel.Children.Add(grid1);

        // Method
        var grid2 = CreateSpecialTaskGrid();
        var label2 = CreateLabelPanel(LangKeys.SpecialTask_RequestMethod, null, null, useI18n: true);
        label2.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label2, 0);
        grid2.Children.Add(label2);

        var methods = new List<string> { "GET", "POST" };
        var methodCombo = new ComboBox
        {
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            ItemsSource = methods,
            SelectedIndex = Math.Max(0, methods.IndexOf((string?)param["method"] ?? "GET")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        BindIdleEnabled(methodCombo);
        methodCombo.SelectionChanged += (_, _) =>
        {
            if (methodCombo.SelectedIndex >= 0)
            {
                param["method"] = methods[methodCombo.SelectedIndex];
                UpdateActionParam(dragItem, param);
            }
        };
        Grid.SetColumn(methodCombo, 1);
        AddResponsiveBehavior(grid2, label2, methodCombo);
        grid2.Children.Add(methodCombo);
        panel.Children.Add(grid2);

        // Body
        var grid3 = CreateSpecialTaskGrid();
        var label3 = CreateLabelPanel(LangKeys.SpecialTask_RequestBody, null, null, useI18n: true);
        label3.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label3, 0);
        grid3.Children.Add(label3);

        var bodyBox = new TextBox
        {
            Text = (string?)param["body"] ?? "",
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 100,
        };
        BindIdleEnabled(bodyBox);
        bodyBox.TextChanged += (_, _) =>
        {
            param["body"] = bodyBox.Text ?? "";
            UpdateActionParam(dragItem, param);
        };
        Grid.SetColumn(bodyBox, 1);
        AddResponsiveBehavior(grid3, label3, bodyBox);
        grid3.Children.Add(bodyBox);
        panel.Children.Add(grid3);

        // Content-Type
        var grid4 = CreateSpecialTaskGrid();
        var label4 = CreateLabelPanel("Content-Type", null, null);
        label4.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(label4, 0);
        grid4.Children.Add(label4);

        var ctBox = new TextBox
        {
            Text = (string?)param["content_type"] ?? "application/json",
            MinWidth = 120,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        BindIdleEnabled(ctBox);
        ctBox.TextChanged += (_, _) =>
        {
            param["content_type"] = ctBox.Text ?? "";
            UpdateActionParam(dragItem, param);
        };
        Grid.SetColumn(ctBox, 1);
        AddResponsiveBehavior(grid4, label4, ctBox);
        grid4.Children.Add(ctBox);
        panel.Children.Add(grid4);
    }

    #endregion
}






