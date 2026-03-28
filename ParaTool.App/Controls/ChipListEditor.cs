using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

/// <summary>
/// Semicolon-separated string displayed as removable colored chips.
/// Typing text and pressing Space/Enter/; creates a new chip.
/// </summary>
public class ChipListEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ChipListEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> ChipColorProperty =
        AvaloniaProperty.Register<ChipListEditor, string>(nameof(ChipColor), "#E67E22");

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string ChipColor
    {
        get => GetValue(ChipColorProperty);
        set => SetValue(ChipColorProperty, value);
    }

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal };
    private readonly TextBox _input;
    private bool _updating;

    public ChipListEditor()
    {
        _input = new TextBox
        {
            FontSize = 12,
            Padding = new Thickness(6, 4),
            MinWidth = 80,
            Watermark = "Type and press Enter...",
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _input.KeyDown += OnInputKeyDown;
        _panel.Children.Add(_input);

        Content = new Border
        {
            Child = _panel,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4, 2),
        };

        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty && !_updating)
                Rebuild();
        };
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space || (e.Key == Key.OemSemicolon))
        {
            var text = _input.Text?.Trim().TrimEnd(';');
            if (!string.IsNullOrEmpty(text))
            {
                var current = Text ?? "";
                _updating = true;
                Text = string.IsNullOrEmpty(current) ? text : $"{current};{text}";
                _updating = false;
                _input.Text = "";
                Rebuild();
            }
            e.Handled = true;
        }
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        var raw = Text ?? "";
        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chipColor = Color.Parse(ChipColor);

        foreach (var part in parts)
        {
            var chip = CreateChip(part, chipColor);
            _panel.Children.Add(chip);
        }

        _panel.Children.Add(_input);
    }

    private Border CreateChip(string value, Color color)
    {
        var colorBrush = new SolidColorBrush(color);

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = colorBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var removeBtn = new Button
        {
            Content = "×", FontSize = 10,
            Padding = new Thickness(3, 0),
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = ThemeBrushes.TextMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        removeBtn.Click += (_, _) =>
        {
            var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            parts.Remove(value);
            _updating = true;
            Text = string.Join(";", parts);
            _updating = false;
            Rebuild();
        };
        stack.Children.Add(removeBtn);

        return new Border
        {
            Child = stack,
            Background = new SolidColorBrush(color, 0.15),
            BorderBrush = colorBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3),
            Margin = new Thickness(2),
        };
    }
}
