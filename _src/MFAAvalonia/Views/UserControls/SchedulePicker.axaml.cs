using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using MFAAvalonia.Helper.ValueType;
using System;
using System.Collections.Generic;

namespace MFAAvalonia.Views.UserControls;

public class SchedulePicker : TemplatedControl
{
    private EventHandler<LanguageHelper.LanguageEventArgs>? _languageChangedHandler;

    #region Styled Properties

    public static readonly StyledProperty<TimerScheduleConfig?> ScheduleConfigProperty =
        AvaloniaProperty.Register<SchedulePicker, TimerScheduleConfig?>(
            nameof(ScheduleConfig), defaultBindingMode: BindingMode.TwoWay);

    public TimerScheduleConfig? ScheduleConfig
    {
        get => GetValue(ScheduleConfigProperty);
        set => SetValue(ScheduleConfigProperty, value);
    }

    public static readonly DirectProperty<SchedulePicker, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<SchedulePicker, string>(
            nameof(DisplayText),
            o => o.DisplayText);

    private string _displayText = string.Empty;

    public string DisplayText
    {
        get => _displayText;
        private set => SetAndRaise(DisplayTextProperty, ref _displayText, value);
    }

    public static readonly DirectProperty<SchedulePicker, bool> IsDailyModeProperty =
        AvaloniaProperty.RegisterDirect<SchedulePicker, bool>(
            nameof(IsDailyMode),
            o => o.IsDailyMode,
            (o, v) => o.IsDailyMode = v);

    private bool _isDailyMode = true;

    public bool IsDailyMode
    {
        get => _isDailyMode;
        set
        {
            if (SetAndRaise(IsDailyModeProperty, ref _isDailyMode, value) && value)
            {
                SetScheduleType(TimerScheduleType.Daily);
            }
        }
    }

    public static readonly DirectProperty<SchedulePicker, bool> IsWeeklyModeProperty =
        AvaloniaProperty.RegisterDirect<SchedulePicker, bool>(
            nameof(IsWeeklyMode),
            o => o.IsWeeklyMode,
            (o, v) => o.IsWeeklyMode = v);

    private bool _isWeeklyMode;

    public bool IsWeeklyMode
    {
        get => _isWeeklyMode;
        set
        {
            if (SetAndRaise(IsWeeklyModeProperty, ref _isWeeklyMode, value) && value)
            {
                SetScheduleType(TimerScheduleType.Weekly);
            }
        }
    }

    public static readonly DirectProperty<SchedulePicker, bool> IsMonthlyModeProperty =
        AvaloniaProperty.RegisterDirect<SchedulePicker, bool>(
            nameof(IsMonthlyMode),
            o => o.IsMonthlyMode,
            (o, v) => o.IsMonthlyMode = v);

    private bool _isMonthlyMode;

    public bool IsMonthlyMode
    {
        get => _isMonthlyMode;
        set
        {
            if (SetAndRaise(IsMonthlyModeProperty, ref _isMonthlyMode, value) && value)
            {
                SetScheduleType(TimerScheduleType.Monthly);
            }
        }
    }

    #endregion

    #region Template Parts

    private CheckBox? _monCheckBox;
    private CheckBox? _tueCheckBox;
    private CheckBox? _wedCheckBox;
    private CheckBox? _thuCheckBox;
    private CheckBox? _friCheckBox;
    private CheckBox? _satCheckBox;
    private CheckBox? _sunCheckBox;
    private Button? _selectWorkdaysButton;
    private Button? _selectWeekendsButton;
    private Button? _selectAllWeekButton;
    private Button? _deselectAllWeekButton;
    private UniformGrid? _monthDaysGrid;
    private Button? _selectAllMonthButton;
    private Button? _deselectAllMonthButton;
    private readonly List<CheckBox> _monthDayCheckBoxes = new();

    #endregion

    static SchedulePicker()
    {
        ScheduleConfigProperty.Changed.AddClassHandler<SchedulePicker>((x, e) => x.OnScheduleConfigChanged(e));
    }

    private void OnScheduleConfigChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is TimerScheduleConfig config)
        {
            UpdateUIFromConfig(config);
        }
    }

    private void UpdateUIFromConfig(TimerScheduleConfig config)
    {
        //更新模式选择
        _isDailyMode = config.ScheduleType == TimerScheduleType.Daily;
        _isWeeklyMode = config.ScheduleType == TimerScheduleType.Weekly;
        _isMonthlyMode = config.ScheduleType == TimerScheduleType.Monthly;

        RaisePropertyChanged(IsDailyModeProperty, !_isDailyMode, _isDailyMode);
        RaisePropertyChanged(IsWeeklyModeProperty, !_isWeeklyMode, _isWeeklyMode);
        RaisePropertyChanged(IsMonthlyModeProperty, !_isMonthlyMode, _isMonthlyMode);

        // 更新周选择
        UpdateWeekCheckBoxes(config.SelectedDaysOfWeek);

        // 更新月选择
        UpdateMonthCheckBoxes(config.SelectedDaysOfMonth);

        // 更新显示文本
        UpdateDisplayText();
    }

    private void UpdateWeekCheckBoxes(HashSet<DayOfWeek> selectedDays)
    {
        if (_monCheckBox != null) _monCheckBox.IsChecked = selectedDays.Contains(DayOfWeek.Monday);
        if (_tueCheckBox != null) _tueCheckBox.IsChecked = selectedDays.Contains(DayOfWeek.Tuesday);
        if (_wedCheckBox != null) _wedCheckBox.IsChecked = selectedDays.Contains(DayOfWeek.Wednesday);
        if (_thuCheckBox != null) _thuCheckBox.IsChecked = selectedDays.Contains(DayOfWeek.Thursday);
        if (_friCheckBox != null) _friCheckBox.IsChecked = selectedDays.Contains(DayOfWeek.Friday);
        if (_satCheckBox != null) _satCheckBox.IsChecked = selectedDays.Contains(DayOfWeek.Saturday);
        if (_sunCheckBox != null) _sunCheckBox.IsChecked = selectedDays.Contains(DayOfWeek.Sunday);
    }

    private void UpdateMonthCheckBoxes(HashSet<int> selectedDays)
    {
        foreach (var checkBox in _monthDayCheckBoxes)
        {
            if (checkBox.Tag is int day)
            {
                checkBox.IsChecked = selectedDays.Contains(day);
            }
        }
    }

    private void SetScheduleType(TimerScheduleType type)
    {
        if (ScheduleConfig != null)
        {
            ScheduleConfig.ScheduleType = type;
            UpdateDisplayText();
            OnScheduleChanged();
        }
    }

    private void UpdateDisplayText()
    {
        DisplayText = ScheduleConfig?.GetDisplayText() ?? Helper.LangKeys.ScheduleDaily.ToLocalization();
    }

    private void OnScheduleChanged()
    {
        // 触发属性变更通知，强制更新绑定
        // 由于设置同一个对象引用时，Avalonia 绑定系统不会检测到变化
        // 需要先清除值再重新设置，以强制触发双向绑定更新 ViewModel
        var config = ScheduleConfig;
        if (config != null)
        {
            ClearValue(ScheduleConfigProperty);
            SetValue(ScheduleConfigProperty, config);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        // 取消之前的事件订阅
        UnsubscribeEvents();

        // 注册语言变化事件
        if (_languageChangedHandler == null)
        {
            _languageChangedHandler = OnLanguageChanged;
            LanguageHelper.LanguageChanged += _languageChangedHandler;
        }

        base.OnApplyTemplate(e);

        // 获取模板部件
        _monCheckBox = e.NameScope.Find<CheckBox>("PART_Mon");
        _tueCheckBox = e.NameScope.Find<CheckBox>("PART_Tue");
        _wedCheckBox = e.NameScope.Find<CheckBox>("PART_Wed");
        _thuCheckBox = e.NameScope.Find<CheckBox>("PART_Thu");
        _friCheckBox = e.NameScope.Find<CheckBox>("PART_Fri");
        _satCheckBox = e.NameScope.Find<CheckBox>("PART_Sat");
        _sunCheckBox = e.NameScope.Find<CheckBox>("PART_Sun");
        _selectWorkdaysButton = e.NameScope.Find<Button>("PART_SelectWorkdays");
        _selectWeekendsButton = e.NameScope.Find<Button>("PART_SelectWeekends");
        _selectAllWeekButton = e.NameScope.Find<Button>("PART_SelectAllWeek");
        _deselectAllWeekButton = e.NameScope.Find<Button>("PART_DeselectAllWeek");
        _monthDaysGrid = e.NameScope.Find<UniformGrid>("PART_MonthDaysGrid");
        _selectAllMonthButton = e.NameScope.Find<Button>("PART_SelectAllMonth");
        _deselectAllMonthButton = e.NameScope.Find<Button>("PART_DeselectAllMonth");

        // 初始化月份日期选择器
        InitializeMonthDaysGrid();

        // 订阅事件
        SubscribeEvents();

        // 从配置更新UI
        if (ScheduleConfig != null)
        {
            UpdateUIFromConfig(ScheduleConfig);
        }
    }

    private void InitializeMonthDaysGrid()
    {
        if (_monthDaysGrid == null) return;

        _monthDaysGrid.Children.Clear();
        _monthDayCheckBoxes.Clear();

        for (int day = 1; day <= 31; day++)
        {
            var checkBox = new CheckBox
            {
                Content = day.ToString(),
                Tag = day,
                Margin = new Thickness(2)
            };
            checkBox.IsCheckedChanged += OnMonthDayCheckBoxChanged;
            _monthDayCheckBoxes.Add(checkBox);
            _monthDaysGrid.Children.Add(checkBox);
        }
    }

    private void SubscribeEvents()
    {
        if (_monCheckBox != null) _monCheckBox.IsCheckedChanged += OnWeekDayCheckBoxChanged;
        if (_tueCheckBox != null) _tueCheckBox.IsCheckedChanged += OnWeekDayCheckBoxChanged;
        if (_wedCheckBox != null) _wedCheckBox.IsCheckedChanged += OnWeekDayCheckBoxChanged;
        if (_thuCheckBox != null) _thuCheckBox.IsCheckedChanged += OnWeekDayCheckBoxChanged;
        if (_friCheckBox != null) _friCheckBox.IsCheckedChanged += OnWeekDayCheckBoxChanged;
        if (_satCheckBox != null) _satCheckBox.IsCheckedChanged += OnWeekDayCheckBoxChanged;
        if (_sunCheckBox != null) _sunCheckBox.IsCheckedChanged += OnWeekDayCheckBoxChanged;
        if (_selectWorkdaysButton != null) _selectWorkdaysButton.Click += OnSelectWorkdaysClick;
        if (_selectWeekendsButton != null) _selectWeekendsButton.Click += OnSelectWeekendsClick;
        if (_selectAllWeekButton != null) _selectAllWeekButton.Click += OnSelectAllWeekClick;
        if (_deselectAllWeekButton != null) _deselectAllWeekButton.Click += OnDeselectAllWeekClick;
        if (_selectAllMonthButton != null) _selectAllMonthButton.Click += OnSelectAllMonthClick;
        if (_deselectAllMonthButton != null) _deselectAllMonthButton.Click += OnDeselectAllMonthClick;
    }

    private void UnsubscribeEvents()
    {
        if (_monCheckBox != null) _monCheckBox.IsCheckedChanged -= OnWeekDayCheckBoxChanged;
        if (_tueCheckBox != null) _tueCheckBox.IsCheckedChanged -= OnWeekDayCheckBoxChanged;
        if (_wedCheckBox != null) _wedCheckBox.IsCheckedChanged -= OnWeekDayCheckBoxChanged;
        if (_thuCheckBox != null) _thuCheckBox.IsCheckedChanged -= OnWeekDayCheckBoxChanged;
        if (_friCheckBox != null) _friCheckBox.IsCheckedChanged -= OnWeekDayCheckBoxChanged;
        if (_satCheckBox != null) _satCheckBox.IsCheckedChanged -= OnWeekDayCheckBoxChanged;
        if (_sunCheckBox != null) _sunCheckBox.IsCheckedChanged -= OnWeekDayCheckBoxChanged;
        if (_selectWorkdaysButton != null) _selectWorkdaysButton.Click -= OnSelectWorkdaysClick;
        if (_selectWeekendsButton != null) _selectWeekendsButton.Click -= OnSelectWeekendsClick;
        if (_selectAllWeekButton != null) _selectAllWeekButton.Click -= OnSelectAllWeekClick;
        if (_deselectAllWeekButton != null) _deselectAllWeekButton.Click -= OnDeselectAllWeekClick;
        if (_selectAllMonthButton != null) _selectAllMonthButton.Click -= OnSelectAllMonthClick;
        if (_deselectAllMonthButton != null) _deselectAllMonthButton.Click -= OnDeselectAllMonthClick;

        foreach (var checkBox in _monthDayCheckBoxes)
        {
            checkBox.IsCheckedChanged -= OnMonthDayCheckBoxChanged;
        }
    }private void OnLanguageChanged(object? sender, LanguageHelper.LanguageEventArgs e)
    {
        UpdateDisplayText();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // 取消语言变化事件订阅
        if (_languageChangedHandler != null)
        {
            LanguageHelper.LanguageChanged -= _languageChangedHandler;
            _languageChangedHandler = null;
        }
    }

    private void OnWeekDayCheckBoxChanged(object? sender, RoutedEventArgs e)
    {
        if (ScheduleConfig == null) return;

        var selectedDays = new HashSet<DayOfWeek>();
        if (_monCheckBox?.IsChecked == true) selectedDays.Add(DayOfWeek.Monday);
        if (_tueCheckBox?.IsChecked == true) selectedDays.Add(DayOfWeek.Tuesday);
        if (_wedCheckBox?.IsChecked == true) selectedDays.Add(DayOfWeek.Wednesday);
        if (_thuCheckBox?.IsChecked == true) selectedDays.Add(DayOfWeek.Thursday);
        if (_friCheckBox?.IsChecked == true) selectedDays.Add(DayOfWeek.Friday);
        if (_satCheckBox?.IsChecked == true) selectedDays.Add(DayOfWeek.Saturday);
        if (_sunCheckBox?.IsChecked == true) selectedDays.Add(DayOfWeek.Sunday);

        ScheduleConfig.SelectedDaysOfWeek = selectedDays;
        UpdateDisplayText();
        OnScheduleChanged();
    }

    private void OnMonthDayCheckBoxChanged(object? sender, RoutedEventArgs e)
    {
        if (ScheduleConfig == null) return;

        var selectedDays = new HashSet<int>();
        foreach (var checkBox in _monthDayCheckBoxes)
        {
            if (checkBox.IsChecked == true && checkBox.Tag is int day)
            {
                selectedDays.Add(day);
            }
        }

        ScheduleConfig.SelectedDaysOfMonth = selectedDays;
        UpdateDisplayText();
        OnScheduleChanged();
    }

    private void OnSelectWorkdaysClick(object? sender, RoutedEventArgs e)
    {
        if (_monCheckBox != null) _monCheckBox.IsChecked = true;
        if (_tueCheckBox != null) _tueCheckBox.IsChecked = true;
        if (_wedCheckBox != null) _wedCheckBox.IsChecked = true;
        if (_thuCheckBox != null) _thuCheckBox.IsChecked = true;
        if (_friCheckBox != null) _friCheckBox.IsChecked = true;
        if (_satCheckBox != null) _satCheckBox.IsChecked = false;
        if (_sunCheckBox != null) _sunCheckBox.IsChecked = false;
    }

    private void OnSelectWeekendsClick(object? sender, RoutedEventArgs e)
    {
        if (_monCheckBox != null) _monCheckBox.IsChecked = false;
        if (_tueCheckBox != null) _tueCheckBox.IsChecked = false;
        if (_wedCheckBox != null) _wedCheckBox.IsChecked = false;
        if (_thuCheckBox != null) _thuCheckBox.IsChecked = false;
        if (_friCheckBox != null) _friCheckBox.IsChecked = false;
        if (_satCheckBox != null) _satCheckBox.IsChecked = true;
        if (_sunCheckBox != null) _sunCheckBox.IsChecked = true;
    }

    private void OnSelectAllWeekClick(object? sender, RoutedEventArgs e)
    {
        if (_monCheckBox != null) _monCheckBox.IsChecked = true;
        if (_tueCheckBox != null) _tueCheckBox.IsChecked = true;
        if (_wedCheckBox != null) _wedCheckBox.IsChecked = true;
        if (_thuCheckBox != null) _thuCheckBox.IsChecked = true;
        if (_friCheckBox != null) _friCheckBox.IsChecked = true;
        if (_satCheckBox != null) _satCheckBox.IsChecked = true;
        if (_sunCheckBox != null) _sunCheckBox.IsChecked = true;
    }

    private void OnDeselectAllWeekClick(object? sender, RoutedEventArgs e)
    {
        if (_monCheckBox != null) _monCheckBox.IsChecked = false;
        if (_tueCheckBox != null) _tueCheckBox.IsChecked = false;
        if (_wedCheckBox != null) _wedCheckBox.IsChecked = false;
        if (_thuCheckBox != null) _thuCheckBox.IsChecked = false;
        if (_friCheckBox != null) _friCheckBox.IsChecked = false;
        if (_satCheckBox != null) _satCheckBox.IsChecked = false;
        if (_sunCheckBox != null) _sunCheckBox.IsChecked = false;
    }

    private void OnSelectAllMonthClick(object? sender, RoutedEventArgs e)
    {
        foreach (var checkBox in _monthDayCheckBoxes)
        {
            checkBox.IsChecked = true;
        }
    }

    private void OnDeselectAllMonthClick(object? sender, RoutedEventArgs e)
    {
        foreach (var checkBox in _monthDayCheckBoxes)
        {
            checkBox.IsChecked = false;
        }
    }
}
