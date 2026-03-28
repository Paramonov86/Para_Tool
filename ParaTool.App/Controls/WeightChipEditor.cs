using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

/// <summary>
/// Odometer drum chip for weight. Displays current value; click to expand a drum
/// that overlays the chip — neighbor values fade above/below with gradient mask.
/// Scroll wheel spins the drum with inertia.
/// </summary>
public class WeightChipEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<WeightChipEditor, string?>(nameof(Text),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<WeightChipEditor, double>(nameof(Step), 0.1);

    public static readonly StyledProperty<double> MinValueProperty =
        AvaloniaProperty.Register<WeightChipEditor, double>(nameof(MinValue), 0.0);

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<WeightChipEditor, double>(nameof(MaxValue), 50.0);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double MinValue { get => GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
    public double MaxValue { get => GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    private const int SidesVisible = 3; // rows above + below center
    private const int TotalRows = SidesVisible * 2 + 1; // 7
    private const double RowH = 24;
    private const double ChipH = 32;

    private readonly Border _chip;
    private readonly TextBlock _valueText;

    private Popup? _popup;
    private TextBlock[]? _drumLabels;
    private double _currentValue;

    // Inertia state
    private double _velocity;
    private double _accumulator;
    private DispatcherTimer? _inertiaTimer;
    private DateTime _lastScrollTime;

    public WeightChipEditor()
    {
        _valueText = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeBrushes.TextPrimary,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        _chip = new Border
        {
            Child = _valueText,
            MinWidth = 48,
            Height = ChipH,
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(8),
            Background = ThemeBrushes.InputBg,
            BorderBrush = ThemeBrushes.BorderSubtle,
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        _chip.PointerPressed += OnChipPressed;
        _chip.PointerEntered += (_, _) => _chip.Background = ThemeBrushes.HoverBg;
        _chip.PointerExited += (_, _) => _chip.Background = ThemeBrushes.InputBg;

        Content = _chip;

        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty) UpdateChipText();
        };
        UpdateChipText();
    }

    private void UpdateChipText()
    {
        var val = Text?.Trim() ?? "";
        _valueText.Text = string.IsNullOrEmpty(val) ? "—" : val;
        _valueText.Foreground = string.IsNullOrEmpty(val)
            ? ThemeBrushes.TextMuted : ThemeBrushes.TextPrimary;
    }

    private static string Fmt(double v) =>
        v == (int)v ? ((int)v).ToString()
        : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private double ParseCurrent()
    {
        var raw = (Text?.Trim() ?? "").Replace(',', '.');
        return double.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    // ── Open drum ──────────────────────────────────────────────

    private void OnChipPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_popup?.IsOpen == true) return;
        _currentValue = ParseCurrent();
        _velocity = 0;
        _accumulator = 0;

        // Opacity curve: center = 1, edges fade out
        double[] opacities = [0.0, 0.12, 0.35, 1.0, 0.35, 0.12, 0.0];

        _drumLabels = new TextBlock[TotalRows];
        var drumStack = new StackPanel { Spacing = 0 };

        for (int i = 0; i < TotalRows; i++)
        {
            var isCenter = i == SidesVisible;
            var tb = new TextBlock
            {
                FontSize = isCenter ? 15 : 13,
                FontWeight = isCenter ? FontWeight.Bold : FontWeight.Normal,
                Foreground = ThemeBrushes.TextPrimary,
                Opacity = opacities[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Height = RowH,
                LineHeight = RowH,
            };
            _drumLabels[i] = tb;
            drumStack.Children.Add(tb);
        }

        RefreshDrum();

        // Gradient fade overlays
        var bg = ThemeBrushes.CardBg.Color;
        var bgTransp = Color.FromArgb(0, bg.R, bg.G, bg.B);

        var topGrad = new Border
        {
            Height = RowH * 2,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new(bg, 0), new(bgTransp, 1) }
            }
        };

        var bottomGrad = new Border
        {
            Height = RowH * 2,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new(bgTransp, 0), new(bg, 1) }
            }
        };

        // Subtle accent highlight on the center row
        var accentColor = ThemeBrushes.Accent.Color;
        var centerBar = new Border
        {
            Height = RowH + 2,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(accentColor, 0.10),
        };

        var grid = new Grid
        {
            Children = { drumStack, centerBar, topGrad, bottomGrad }
        };

        var popupBorder = new Border
        {
            Child = grid,
            Background = ThemeBrushes.CardBg,
            BorderBrush = ThemeBrushes.BorderSubtle,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 2),
            MinWidth = _chip.Bounds.Width > 0 ? _chip.Bounds.Width : 56,
            BoxShadow = BoxShadows.Parse("0 4 16 0 #30000000"),
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
        };

        popupBorder.PointerWheelChanged += OnDrumWheel;

        // PlacementMode.Top places popup above the chip's top edge.
        // We need the drum center to align with the chip center.
        // Top mode: popup bottom = chip top → offset down by (drumH - drumH/2 - chipH/2)
        var drumH = TotalRows * RowH + 4;
        var vOff = drumH / 2 - ChipH / 2;

        _popup = new Popup
        {
            Child = popupBorder,
            PlacementTarget = _chip,
            Placement = PlacementMode.Top,
            VerticalOffset = vOff,
            HorizontalOffset = -13,
            IsLightDismissEnabled = true,
        };

        _popup.Closed += OnPopupClosed;
        _popup.IsOpen = true;
    }

    // ── Drum rendering ─────────────────────────────────────────

    private void RefreshDrum()
    {
        if (_drumLabels == null) return;
        double[] opacities = [0.0, 0.12, 0.35, 1.0, 0.35, 0.12, 0.0];

        for (int i = 0; i < TotalRows; i++)
        {
            int offset = i - SidesVisible; // -3..+3
            double val = Math.Round(_currentValue + offset * Step, 4);
            if (val < MinValue - 0.001 || val > MaxValue + 0.001)
            {
                _drumLabels[i].Text = "";
                _drumLabels[i].Opacity = 0;
            }
            else
            {
                _drumLabels[i].Text = Fmt(Math.Clamp(val, MinValue, MaxValue));
                _drumLabels[i].Opacity = opacities[i];
            }
        }
    }

    private void Nudge(int steps)
    {
        var next = Math.Round(_currentValue + steps * Step, 4);
        next = Math.Clamp(next, MinValue, MaxValue);
        if (Math.Abs(next - _currentValue) < 1e-9) return;
        _currentValue = next;
        RefreshDrum();
    }

    // ── Scroll + inertia ───────────────────────────────────────

    private void OnDrumWheel(object? sender, PointerWheelEventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt = (now - _lastScrollTime).TotalSeconds;
        _lastScrollTime = now;

        // Scroll up → bigger values (positive direction)
        int dir = e.Delta.Y > 0 ? 1 : -1;
        Nudge(dir);

        // Accumulate velocity for inertia during fast scroll
        if (dt < 0.12)
            _velocity = Math.Clamp(_velocity + dir * 3.0, -25, 25);
        else
            _velocity = dir * 1.8;

        _accumulator = 0;
        StartInertia();
        e.Handled = true;
    }

    private void StartInertia()
    {
        if (_inertiaTimer != null) return;
        _inertiaTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _inertiaTimer.Tick += OnInertiaTick;
        _inertiaTimer.Start();
    }

    private void OnInertiaTick(object? sender, EventArgs e)
    {
        _velocity *= 0.86; // friction
        _accumulator += _velocity * 0.016;

        while (Math.Abs(_accumulator) >= 1.0)
        {
            int dir = _accumulator > 0 ? 1 : -1;
            Nudge(dir);
            _accumulator -= dir;
        }

        if (Math.Abs(_velocity) < 0.4)
        {
            StopInertia();
        }
    }

    private void StopInertia()
    {
        _velocity = 0;
        _accumulator = 0;
        _inertiaTimer?.Stop();
        _inertiaTimer = null;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        StopInertia();
        Text = Fmt(_currentValue);
        _drumLabels = null;
        _popup = null;
    }
}
