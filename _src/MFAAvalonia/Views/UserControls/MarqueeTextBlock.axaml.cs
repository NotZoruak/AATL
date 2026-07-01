using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MFAAvalonia.Views.UserControls;

/// <summary>
/// 跑马灯文本控件 - 当文本超出容器宽度且鼠标悬停时自动滚动显示
/// 实现无缝循环：文字向左滚动，结尾后紧跟着开头，形成连续循环
/// </summary>
public class MarqueeTextBlock : TemplatedControl
{
    private const string ElementCanvas = "PART_Canvas";
    private const string ElementTextBlock1 = "PART_TextBlock1";
    private const string ElementTextBlock2 = "PART_TextBlock2";
    
    /// <summary>
    /// 两段文本之间的间距（用于区分是同一句话）
    /// </summary>
    private const double TextGap = 70;
    
    /// <summary>
    /// 滚动速度（像素/秒）
    /// </summary>
    private const double ScrollSpeed = 50;

    private Canvas? _canvas;
    private TextBlock? _textBlock1;
    private TextBlock? _textBlock2;
    private DispatcherTimer? _animationTimer;
    private double _scrollPosition;
    private double _textWidth;
    private double _containerWidth;
    private bool _needsScrolling;
    private bool _isHovering;
    private DateTime _lastTickTime;
    private Button? _parentButton;
    private bool _isInsideButton;

    #region Styled Properties

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarqueeTextBlock, string?>(nameof(Text));

    public static readonly StyledProperty<VerticalAlignment> VerticalContentAlignmentProperty =
        AvaloniaProperty.Register<MarqueeTextBlock, VerticalAlignment>(nameof(VerticalContentAlignment), VerticalAlignment.Center);

    /// <summary>
    /// 显示的文本
    /// </summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// 垂直内容对齐
    /// </summary>
    public VerticalAlignment VerticalContentAlignment
    {
        get => GetValue(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    #endregion

    public MarqueeTextBlock()
    {
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _animationTimer.Tick += OnAnimationTick;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        _canvas = e.NameScope.Find<Canvas>(ElementCanvas);
        _textBlock1 = e.NameScope.Find<TextBlock>(ElementTextBlock1);
        _textBlock2 = e.NameScope.Find<TextBlock>(ElementTextBlock2);

        if (_textBlock1 != null)
        {
            _textBlock1.PropertyChanged += OnTextBlockPropertyChanged;
        }

        UpdateMeasurements();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _containerWidth = e.NewSize.Width;
        UpdateMeasurements();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _parentButton = this.FindAncestorOfType<Button>();
        _isInsideButton = _parentButton != null;
        if (_isInsideButton)
        {
            _parentButton!.PointerMoved += OnParentPointerMoved;
            _parentButton.PointerExited += OnParentPointerExited;
        }
    }

    private void OnParentPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_canvas == null) return;
        var pos = e.GetPosition(_canvas);
        var isOver = new Rect(0, 0, _canvas.Width, _canvas.Height).Contains(pos);
        if (isOver && !_isHovering)
        {
            _isHovering = true;
            if (_needsScrolling) StartAnimation();
        }
        else if (!isOver && _isHovering)
        {
            _isHovering = false;
            StopAnimation();
            ResetPosition();
        }
    }

    private void OnParentPointerExited(object? sender, PointerEventArgs e)
    {
        if (_isHovering)
        {
            _isHovering = false;
            StopAnimation();
            ResetPosition();
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (_isInsideButton) return;
        _isHovering = true;
        if (_needsScrolling) StartAnimation();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_isInsideButton) return;
        _isHovering = false;
        StopAnimation();
        ResetPosition();
    }

    private void OnTextBlockPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty || e.Property == TextBlock.TextProperty)
        {
            UpdateMeasurements();
        }
    }

    private void UpdateMeasurements()
    {
        if (_textBlock1 == null)
            return;

        // 测量文本实际宽度
        _textBlock1.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _textWidth = _textBlock1.DesiredSize.Width;
        
        // 获取容器宽度
        _containerWidth = Bounds.Width;
        
        // 设置Canvas的尺寸以匹配文本
        if (_canvas != null)
        {
            _canvas.Width = _containerWidth;
            _canvas.Height = _textBlock1.DesiredSize.Height;
        }
        
        // 判断是否需要滚动（文本宽度大于容器宽度）
        _needsScrolling = _textWidth > _containerWidth && _containerWidth > 0;
        
        // 更新第二个文本块的可见性
        if (_textBlock2 != null)
        {
            _textBlock2.IsVisible = _needsScrolling; // 默认隐藏，动画时才显示
        }
        
        // 如果不需要滚动，确保文本位置正确
        if (!_needsScrolling)
        {
            ResetPosition();
        }
    }

    private void StartAnimation()
    {
        if (_animationTimer == null || !_needsScrolling)
            return;

        _scrollPosition = 0;
        _lastTickTime = DateTime.Now;
        
        // 显示第二个文本块用于无缝循环
        if (_textBlock2 != null)
        {
            _textBlock2.IsVisible = true;
        }
        
        UpdateTextPositions();
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
    }

    private void ResetPosition()
    {
        _scrollPosition = 0;
        
        if (_textBlock1 != null)
        {
            Canvas.SetLeft(_textBlock1, 0);
        }
        
        if (_textBlock2 != null)
        {
            _textBlock2.IsVisible = false;
            Canvas.SetLeft(_textBlock2, _textWidth + TextGap);
        }
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (!_isHovering || !_needsScrolling)
        {
            StopAnimation();
            return;
        }

        // 计算实际经过的时间，确保动画平滑
        var now = DateTime.Now;
        double deltaTime = (now - _lastTickTime).TotalSeconds;
        _lastTickTime = now;
        
        // 防止跳跃（比如窗口失焦后恢复）
        if (deltaTime > 0.1)
            deltaTime = 0.016;
        
        double deltaPixels = ScrollSpeed * deltaTime;
        
        _scrollPosition += deltaPixels;
        
        // 一个完整的循环周期 = 文本宽度 + 间距
        double cycleWidth = _textWidth + TextGap;
        
        // 当滚动超过一个周期时，重置（使用取模保持连续性）
        while (_scrollPosition >= cycleWidth)
        {
            _scrollPosition -= cycleWidth;
        }
        
        UpdateTextPositions();
    }

    private void UpdateTextPositions()
    {
        if (_textBlock1 == null || _textBlock2 == null)
            return;

        // 循环周期
        double cycleWidth = _textWidth + TextGap;
        
        // 计算两个文本块的位置
        // text1从位置0开始向左移动
        double pos1 = -_scrollPosition;
        
        // text2紧跟在text1后面，位置 = text1位置 + 文本宽度 + 间距
        double pos2 = pos1 + _textWidth + TextGap;
        
        // 设置位置
        Canvas.SetLeft(_textBlock1, pos1);
        Canvas.SetLeft(_textBlock2, pos2);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_textBlock1 == null)
            return base.MeasureOverride(availableSize);
        
        // 测量文本的实际大小
        _textBlock1.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textSize = _textBlock1.DesiredSize;
        
        // 宽度：如果有限制就用限制，否则用文本宽度
        double width;
        if (double.IsInfinity(availableSize.Width))
        {
            width = textSize.Width;
        }
        else
        {
            width = availableSize.Width;
        }
        
        return new Size(width, textSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _containerWidth = finalSize.Width;
        
        // 重新检查是否需要滚动
        if (_textBlock1 != null)
        {
            _textBlock1.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _textWidth = _textBlock1.DesiredSize.Width;
            _needsScrolling = _textWidth > _containerWidth && _containerWidth > 0;
            
            // 更新Canvas尺寸
            if (_canvas != null)
            {
                _canvas.Width = _containerWidth;
                _canvas.Height = _textBlock1.DesiredSize.Height;
            }
        }
        
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_isInsideButton && _parentButton != null)
        {
            _parentButton.PointerMoved -= OnParentPointerMoved;
            _parentButton.PointerExited -= OnParentPointerExited;
            _parentButton = null;
        }
        _isInsideButton = false;

        base.OnDetachedFromVisualTree(e);
        
        StopAnimation();
        
        if (_textBlock1 != null)
        {
            _textBlock1.PropertyChanged -= OnTextBlockPropertyChanged;
        }
        
        if (_animationTimer != null)
        {
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer = null;
        }
    }
}