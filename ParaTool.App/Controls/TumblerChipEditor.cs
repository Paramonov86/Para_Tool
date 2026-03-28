using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

public class TumblerChipEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TumblerChipEditor, string?>(nameof(Text),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<TumblerChipEditor, double>(nameof(Step), 0.1);

    public static readonly StyledProperty<double> MinValueProperty =
        AvaloniaProperty.Register<TumblerChipEditor, double>(nameof(MinValue), 0.0);

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<TumblerChipEditor, double>(nameof(MaxValue), 999.0);

    public static readonly StyledProperty<string[]?> ItemsProperty =
        AvaloniaProperty.Register<TumblerChipEditor, string[]?>(nameof(Items));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double MinValue { get => GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
    public double MaxValue { get => GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    /// <summary>If set, tumbler scrolls through this list instead of numeric range.</summary>
    public string[]? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    private bool IsListMode => Items is { Length: > 0 };

    private const int SidesCount = 3;
    private const double RowH = 26;
    private const double ChipH = 32;
    private const double Overlap = 10;
    private static readonly double[] SideOpacity = [0.55, 0.25, 0.08];

    private readonly Border _chip;
    private readonly TextBlock _valueText;
    private readonly Panel _root;

    private Border? _upperBg, _lowerBg, _upperGrad, _lowerGrad;
    private readonly List<(Control ctrl, int oldZ)> _liftedAncestors = [];
    private TextBlock[]? _upperLabels, _lowerLabels;
    private bool _drumOpen;
    private double _currentValue;
    private int _currentIndex; // for list mode
    private double _velocity, _accumulator;
    private DispatcherTimer? _inertiaTimer;
    private DateTime _lastScrollTime;

    public TumblerChipEditor()
    {
        _valueText = new TextBlock
        {
            FontSize = 14, FontWeight = FontWeight.SemiBold,
            Foreground = ThemeBrushes.TextPrimary,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            MinWidth = 30,
        };

        _chip = new Border
        {
            Child = _valueText,
            MinWidth = 48, Height = ChipH,
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(8),
            Background = ThemeBrushes.InputBg,
            BorderBrush = ThemeBrushes.BorderSubtle,
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            ZIndex = 10,
        };

        _chip.PointerPressed += OnChipPressed;
        _chip.PointerEntered += (_, _) => { if (!_drumOpen) _chip.Background = ThemeBrushes.HoverBg; };
        _chip.PointerExited += (_, _) => { if (!_drumOpen) _chip.Background = ThemeBrushes.InputBg; };

        _root = new Panel { ClipToBounds = false, Height = ChipH, Children = { _chip } };
        BuildDrumStrips();
        Content = _root;
        ClipToBounds = false;

        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty) UpdateChipText();
            if (e.Property == ItemsProperty) UpdateMinWidthFromItems();
        };
        UpdateChipText();
    }

    private void BuildDrumStrips()
    {
        _upperLabels = new TextBlock[SidesCount];
        _upperStrip = new StackPanel { Spacing = 0 };
        for (int i = 0; i < SidesCount; i++)
        {
            _upperLabels[i] = MakeLabel(SideOpacity[SidesCount - 1 - i]);
            _upperStrip.Children.Add(_upperLabels[i]);
        }

        _lowerLabels = new TextBlock[SidesCount];
        _lowerStrip = new StackPanel { Spacing = 0 };
        for (int i = 0; i < SidesCount; i++)
        {
            _lowerLabels[i] = MakeLabel(SideOpacity[i]);
            _lowerStrip.Children.Add(_lowerLabels[i]);
        }

        var bg = ThemeBrushes.PanelBg.Color;
        var bgSolid = Color.FromArgb(255, bg.R, bg.G, bg.B);
        var bgNone = Color.FromArgb(0, bg.R, bg.G, bg.B);

        var gradH = 3 * RowH + Overlap;

        _upperGrad = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Height = gradH,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new(bgNone, 0), new(bgSolid, 0.35), new(bgSolid, 1) }
            },
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            IsHitTestVisible = false,
            // 5px lower so it tucks nicely under chip rounded corners
            RenderTransform = new TranslateTransform(0, -(3 * RowH) + 10),
            ZIndex = 3, IsVisible = false,
        };

        _upperBg = new Border
        {
            Child = _upperStrip,
            Padding = new Thickness(10, 0),
            IsVisible = false,
            RenderTransform = new TranslateTransform(0, -(SidesCount * RowH)),
            ZIndex = 5,
        };

        _lowerGrad = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Height = gradH,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new(bgSolid, 0), new(bgSolid, 0.65), new(bgNone, 1) }
            },
            CornerRadius = new CornerRadius(0, 0, 6, 6),
            IsHitTestVisible = false,
            RenderTransform = new TranslateTransform(0, ChipH - Overlap),
            ZIndex = 3, IsVisible = false,
        };

        _lowerBg = new Border
        {
            Child = _lowerStrip,
            Padding = new Thickness(10, 0),
            IsVisible = false,
            RenderTransform = new TranslateTransform(0, ChipH),
            ZIndex = 5,
        };

        _root.Children.Insert(0, _upperGrad);
        _root.Children.Insert(1, _lowerGrad);
        _root.Children.Insert(2, _upperBg);
        _root.Children.Insert(3, _lowerBg);
    }

    private StackPanel? _upperStrip, _lowerStrip;

    private static TextBlock MakeLabel(double opacity) => new()
    {
        FontSize = 13, Foreground = ThemeBrushes.TextPrimary,
        Opacity = opacity,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        Height = RowH, LineHeight = RowH,
        MinWidth = 30,
    };

    private void UpdateMinWidthFromItems()
    {
        if (Items is not { Length: > 0 }) return;
        // Measure widest item text to set chip width
        var longest = Items.OrderByDescending(s => s.Length).First();
        var tb = new TextBlock { Text = longest, FontSize = 14, FontWeight = FontWeight.SemiBold };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var w = tb.DesiredSize.Width + 24; // +padding
        _chip.MinWidth = Math.Max(w, 48);
        _valueText.MinWidth = w - 20;
        // Also set min width on drum labels
        if (_upperLabels != null)
            foreach (var l in _upperLabels) l.MinWidth = w - 20;
        if (_lowerLabels != null)
            foreach (var l in _lowerLabels) l.MinWidth = w - 20;
    }

    private void UpdateChipText()
    {
        var val = Text?.Trim() ?? "";
        _valueText.Text = string.IsNullOrEmpty(val) ? "—" : val;
        _valueText.Foreground = string.IsNullOrEmpty(val)
            ? ThemeBrushes.TextMuted : ThemeBrushes.TextPrimary;
    }

    private string Fmt(double v) =>
        Step < 0.1
            ? v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            : (v == (int)v ? ((int)v).ToString()
               : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));

    private double ParseCurrent()
    {
        var raw = (Text?.Trim() ?? "").Replace(',', '.');
        return double.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    // ── Open / Close ───────────────────────────────────────────

    private void OnChipPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_drumOpen) CloseDrum(); else OpenDrum();
        e.Handled = true;
    }

    private void OpenDrum()
    {
        _drumOpen = true;
        if (IsListMode)
        {
            var items = Items!;
            var cur = Text?.Trim() ?? "";
            _currentIndex = Array.FindIndex(items, s => s.Equals(cur, StringComparison.OrdinalIgnoreCase));
            if (_currentIndex < 0) _currentIndex = 0;
        }
        else
        {
            _currentValue = ParseCurrent();
        }
        _velocity = 0;
        _accumulator = 0;

        ShowDimmer();
        LiftAncestors();

        _chip.Background = ThemeBrushes.HoverBg;
        _chip.BorderBrush = ThemeBrushes.Accent;
        _chip.Cursor = new Cursor(StandardCursorType.SizeNorthSouth);

        var chipW = _chip.Bounds.Width > 0 ? _chip.Bounds.Width : 48;
        _upperGrad!.Width = chipW;
        _lowerGrad!.Width = chipW;

        _upperBg!.IsVisible = true;
        _lowerBg!.IsVisible = true;
        _upperGrad.IsVisible = true;
        _lowerGrad.IsVisible = true;
        RefreshDrum();

        SubscribeGlobal();
    }

    private void CloseDrum()
    {
        if (!_drumOpen) return;
        _drumOpen = false;
        StopInertia();
        Text = IsListMode ? Items![_currentIndex] : Fmt(_currentValue);

        HideDimmer();
        RestoreAncestors();

        _chip.Background = ThemeBrushes.InputBg;
        _chip.BorderBrush = ThemeBrushes.BorderSubtle;
        _chip.Cursor = new Cursor(StandardCursorType.Hand);

        _upperBg!.IsVisible = false;
        _lowerBg!.IsVisible = false;
        _upperGrad!.IsVisible = false;
        _lowerGrad!.IsVisible = false;

        UnsubscribeGlobal();
    }

    // ── Dimmer with hole for tumbler ────────────────────────────

    /// <summary>Raise ZIndex on all ancestors so drum overflow draws above sibling blocks.</summary>
    private void LiftAncestors()
    {
        _liftedAncestors.Clear();
        Control? current = this;
        while (current != null)
        {
            if (current.Parent is Window) break;
            var oldZ = current.ZIndex;
            current.ZIndex = 8000;
            _liftedAncestors.Add((current, oldZ));
            current = current.Parent as Control;
        }
    }

    private void RestoreAncestors()
    {
        foreach (var (ctrl, oldZ) in _liftedAncestors)
            ctrl.ZIndex = oldZ;
        _liftedAncestors.Clear();
    }

    private Panel? _dimmerPanel;
    private Border? _creepLayer; // slowly darkens over time
    private DispatcherTimer? _creepTimer;

    private void ShowDimmer()
    {
        if (TopLevel.GetTopLevel(this) is not Window w || w.Content is not Panel panel) return;

        var drumTop = -(SidesCount * RowH);
        var drumBottom = ChipH + SidesCount * RowH;
        var topLeft = _root.TranslatePoint(new Point(-8, drumTop - 6), panel);
        var botRight = _root.TranslatePoint(new Point(_root.Bounds.Width + 8, drumBottom + 6), panel);

        if (!topLeft.HasValue || !botRight.HasValue) return;

        var winW = w.Bounds.Width;
        var winH = w.Bounds.Height;
        var hole = new Rect(topLeft.Value, botRight.Value);
        var fullRect = new RectangleGeometry(new Rect(0, 0, winW, winH));

        // Multiple layers with expanding holes → soft glow/blur around the tumbler
        // Inner layer: tight hole, light dim (the "glow" zone)
        // Middle layer: medium hole, medium dim
        // Outer layer: wide hole, heavy dim (most of the screen)
        var layers = new (double inflate, double alpha)[]
        {
            (0, 0.55),   // core: tight hole, max darkness
            (20, 0.25),  // soft edge 1
            (40, 0.10),  // soft edge 2 — fades into full dim
        };

        _dimmerPanel = new Panel
        {
            ZIndex = 9000,
            IsHitTestVisible = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            [Grid.RowSpanProperty] = 99,
            [Grid.ColumnSpanProperty] = 99,
            Opacity = 0,
        };

        foreach (var (inflate, alpha) in layers)
        {
            var inflatedHole = hole.Inflate(new Thickness(inflate));
            var cutout = new RectangleGeometry(inflatedHole, 12 + inflate / 2, 12 + inflate / 2);
            var clip = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, cutout);

            _dimmerPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Colors.Black, alpha),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Clip = clip,
                IsHitTestVisible = false,
            });
        }

        // Extra layer that creeps toward full black over time
        _creepLayer = new Border
        {
            Background = new SolidColorBrush(Colors.Black, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Clip = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect,
                new RectangleGeometry(hole, 12, 12)),
            IsHitTestVisible = false,
        };
        _dimmerPanel.Children.Add(_creepLayer);

        panel.Children.Add(_dimmerPanel);

        // Ease-in animation: slow start, fast finish (cubic)
        _dimmerPanel.Transitions = [new Avalonia.Animation.DoubleTransition
        {
            Property = OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(250),
            Easing = new Avalonia.Animation.Easings.CubicEaseIn(),
        }];
        Dispatcher.UIThread.Post(() => { if (_dimmerPanel != null) _dimmerPanel.Opacity = 1; });

        // Start creep: +0.25% per second → +0.004 alpha every 16ms tick (60fps)
        _creepTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _creepTimer.Tick += (_, _) =>
        {
            if (_creepLayer?.Background is SolidColorBrush b)
            {
                var next = Math.Min(b.Opacity + 0.000084, 1.0); // 0.5% per sec at 60fps
                _creepLayer.Background = new SolidColorBrush(Colors.Black, next);
            }
        };
        _creepTimer.Start();
    }

    private void HideDimmer()
    {
        _creepTimer?.Stop();
        _creepTimer = null;
        _creepLayer = null;
        if (_dimmerPanel == null) return;
        if (_dimmerPanel.Parent is Panel panel)
            panel.Children.Remove(_dimmerPanel);
        _dimmerPanel = null;
    }

    // ── Global input via tunnel (works above dimmer) ────────────

    private void SubscribeGlobal()
    {
        if (TopLevel.GetTopLevel(this) is not { } tl) return;
        tl.AddHandler(PointerPressedEvent, OnGlobalPress, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        tl.AddHandler(PointerWheelChangedEvent, OnGlobalWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void UnsubscribeGlobal()
    {
        if (TopLevel.GetTopLevel(this) is not { } tl) return;
        tl.RemoveHandler(PointerPressedEvent, OnGlobalPress);
        tl.RemoveHandler(PointerWheelChangedEvent, OnGlobalWheel);
    }

    private Rect GetDrumHitArea()
    {
        var top = -(SidesCount * RowH);
        var bottom = ChipH + SidesCount * RowH;
        return new Rect(-6, top, _root.Bounds.Width + 12, bottom - top);
    }

    private void OnGlobalPress(object? sender, PointerPressedEventArgs e)
    {
        if (!_drumOpen) return;
        CloseDrum();
        e.Handled = true;
    }

    private void OnGlobalWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!_drumOpen) return;

        // Block ALL scrolling while drum is open — prevent page scroll behind dimmer
        e.Handled = true;

        var pos = e.GetPosition(_root);
        if (!GetDrumHitArea().Contains(pos)) return;

        var now = DateTime.UtcNow;
        var dt = (now - _lastScrollTime).TotalSeconds;
        _lastScrollTime = now;

        int dir = e.Delta.Y > 0 ? -1 : 1;
        Nudge(dir);

        // Scale inertia — int/list chips get less momentum
        var impulse = (IsListMode || Step >= 1) ? 0.3 : 1.0;
        if (dt < 0.08)
            _velocity = Math.Clamp(_velocity + dir * 30.0 * impulse, -400 * impulse, 400 * impulse);
        else if (dt < 0.15)
            _velocity = Math.Clamp(_velocity + dir * 12.0 * impulse, -200 * impulse, 200 * impulse);
        else
            _velocity = dir * 0.5 * impulse;

        _accumulator = 0;
        StartInertia();
        e.Handled = true;
    }

    // ── Drum values ────────────────────────────────────────────

    private void RefreshDrum()
    {
        if (_upperLabels == null || _lowerLabels == null) return;

        if (IsListMode)
        {
            var items = Items!;
            for (int i = 0; i < SidesCount; i++)
            {
                int idx = _currentIndex - (SidesCount - i);
                _upperLabels[i].Text = idx >= 0 ? items[idx] : "";
            }
            for (int i = 0; i < SidesCount; i++)
            {
                int idx = _currentIndex + i + 1;
                _lowerLabels[i].Text = idx < items.Length ? items[idx] : "";
            }
            _valueText.Text = items[_currentIndex];
        }
        else
        {
            var step = EffectiveStep;
            for (int i = 0; i < SidesCount; i++)
            {
                double val = Math.Round(_currentValue + (-(SidesCount - i)) * step, 4);
                _upperLabels[i].Text = val >= MinValue ? Fmt(val) : "";
            }
            for (int i = 0; i < SidesCount; i++)
            {
                double val = Math.Round(_currentValue + (i + 1) * step, 4);
                _lowerLabels[i].Text = val <= MaxValue ? Fmt(val) : "";
            }
            _valueText.Text = Fmt(_currentValue);
        }

        _valueText.Foreground = ThemeBrushes.TextPrimary;
    }

    private double EffectiveStep =>
        _currentValue >= 100 ? Math.Max(Step * 100, 1.0) :
        _currentValue >= 10 ? Math.Max(Step * 10, 0.1) :
        Step;

    private void Nudge(int steps)
    {
        if (IsListMode)
        {
            var next = Math.Clamp(_currentIndex + steps, 0, Items!.Length - 1);
            if (next == _currentIndex) return;
            _currentIndex = next;
        }
        else
        {
            var next = Math.Round(_currentValue + steps * EffectiveStep, 4);
            next = Math.Clamp(next, MinValue, MaxValue);
            if (Math.Abs(next - _currentValue) < 1e-9) return;
            _currentValue = next;
        }
        RefreshDrum();
    }

    // ── Inertia ─────────────────────────────────────────────────

    private void StartInertia()
    {
        if (_inertiaTimer != null) return;
        _inertiaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _inertiaTimer.Tick += OnInertiaTick;
        _inertiaTimer.Start();
    }

    private void OnInertiaTick(object? sender, EventArgs e)
    {
        _velocity *= 0.95;
        _accumulator += _velocity * 0.016;

        while (Math.Abs(_accumulator) >= 1.0)
        {
            int dir = _accumulator > 0 ? 1 : -1;
            Nudge(dir);
            _accumulator -= dir;
        }

        if (Math.Abs(_velocity) < 0.5)
            StopInertia();
    }

    private void StopInertia()
    {
        _velocity = 0;
        _accumulator = 0;
        _inertiaTimer?.Stop();
        _inertiaTimer = null;
    }
}
