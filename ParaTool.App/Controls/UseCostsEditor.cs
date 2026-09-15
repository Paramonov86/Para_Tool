
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.Localization;
using ParaTool.App.Services;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

/// <summary>
/// A spell's UseCosts ("ActionPoint:1;SpellSlotsGroup:1:1:3") as coloured resource badges with an
/// amount tumbler (the level for a spell slot) and "+" to add a resource. Text is written only after
/// a user action, and segments nobody touched keep their exact spelling ("ActionPoint:1\t"): an
/// untouched card is compiled verbatim, so re-serializing on load would mark every card edited.
/// </summary>
public class UseCostsEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<UseCostsEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private const string SpellSlots = "SpellSlotsGroup";

    /// <summary>Resources "+" offers, roughly by how often vanilla spells spend them.</summary>
    public static readonly string[] CostResources =
    [
        "ActionPoint", "BonusActionPoint", "ReactionActionPoint", "Movement", SpellSlots,
        "KiPoint", "WildShape", "ChannelOath", "SorceryPoint", "ChannelDivinity", "ArcaneShot",
        "SuperiorityDie", "NaturalRecoveryPoint", "LayOnHandsCharge", "BardicInspiration", "Rage",
        "Bladesong", "StarMapPoint", "CosmicOmen", "WrithingTidePoint", "ArcaneRecoveryPoint",
    ];

    private static readonly Dictionary<string, Color> ResourceColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ActionPoint"] = Color.Parse("#2ECC71"),
        ["BonusActionPoint"] = Color.Parse("#E67E22"),
        ["ReactionActionPoint"] = Color.Parse("#C45AEC"),
        ["Movement"] = Color.Parse("#F1C40F"),
        [SpellSlots] = Color.Parse("#3498DB"),
        ["KiPoint"] = Color.Parse("#1ABC9C"),
        ["WildShape"] = Color.Parse("#A1887F"),
        ["ChannelOath"] = Color.Parse("#F5D76E"),
        ["SorceryPoint"] = Color.Parse("#E056A0"),
        ["ChannelDivinity"] = Color.Parse("#F5D76E"),
        ["ArcaneShot"] = Color.Parse("#5DADE2"),
        ["SuperiorityDie"] = Color.Parse("#CD6155"),
        ["NaturalRecoveryPoint"] = Color.Parse("#58D68D"),
        ["LayOnHandsCharge"] = Color.Parse("#F7DC6F"),
        ["BardicInspiration"] = Color.Parse("#FF79C6"),
        ["Rage"] = Color.Parse("#E74C3C"),
        ["Bladesong"] = Color.Parse("#AF7AC5"),
        ["StarMapPoint"] = Color.Parse("#A569BD"),
        ["CosmicOmen"] = Color.Parse("#A569BD"),
        ["WrithingTidePoint"] = Color.Parse("#48C9B0"),
        ["ArcaneRecoveryPoint"] = Color.Parse("#85C1E9"),
    };

    private static readonly Color OtherColor = Color.Parse("#95A5A6");

    /// <summary>One ";"-separated segment; Raw is returned byte for byte until its numbers change.</summary>
    private sealed class Segment(string raw)
    {
        public string Raw { get; } = raw;
        public string Resource = "";
        public string? Value;
        public int? Amount, Level, OrigAmount, OrigLevel;

        public bool IsSlot => Resource == SpellSlots && Level != null;

        public string Text
        {
            get
            {
                if (Amount == OrigAmount && Level == OrigLevel) return Raw;
                var lead = Raw[..(Raw.Length - Raw.TrimStart().Length)];
                return lead + (IsSlot ? $"{SpellSlots}:{Amount}:{Amount}:{Level}" : $"{Resource}:{Amount}");
            }
        }
    }

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal, ClipToBounds = false };
    private readonly Button _addBtn;
    private List<Segment> _segments = [];
    private bool _updating;

    public UseCostsEditor()
    {
        _addBtn = new Button
        {
            Content = "+",
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(8, 0),
            Background = Brushes.Transparent,
            Foreground = ThemeBrushes.Accent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Built on each click and opened like the condition editor's "+"; a MenuFlyout filled in
        // its Opening event never showed.
        _addBtn.Click += (_, _) => OpenAddMenu();

        Content = _panel;
        ClipToBounds = false;

        PropertyChanged += (_, e) =>
        {
            if (e.Property != TextProperty || _updating) return;
            _segments = Parse(Text);
            Rebuild();
        };
        AttachedToVisualTree += (_, _) =>
        {
            FontScale.ScaleChanged += Rebuild;
            Loc.Instance.PropertyChanged += OnLocChanged;
            Rebuild();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            FontScale.ScaleChanged -= Rebuild;
            Loc.Instance.PropertyChanged -= OnLocChanged;
        };
    }

    private void OnLocChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);

    private static List<Segment> Parse(string? text) =>
        string.IsNullOrWhiteSpace(text) ? [] : text.Split(';').Select(ParseSegment).ToList();

    private static Segment ParseSegment(string raw)
    {
        var seg = new Segment(raw);
        var parts = raw.Trim().Split(':');
        if (parts.Length == 4 && parts[0] == SpellSlots
            && int.TryParse(parts[1], out var amount) && int.TryParse(parts[2], out var same) && amount == same
            && int.TryParse(parts[3], out var level))
        {
            seg.Resource = SpellSlots;
            seg.Amount = seg.OrigAmount = amount;
            seg.Level = seg.OrigLevel = level;
        }
        else if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
        {
            // "Movement:Distance*0.5" keeps its text; only a plain number gets a tumbler.
            seg.Resource = parts[0];
            seg.Value = parts[1];
            if (int.TryParse(parts[1], out var n)) seg.Amount = seg.OrigAmount = n;
        }
        return seg;
    }

    private void WriteText()
    {
        _updating = true;
        Text = string.Join(";", _segments.Select(s => s.Text)).TrimStart();
        _updating = false;
    }

    private static string ResourceLabel(string resource)
    {
        var key = $"resource.{resource}";
        var label = Loc.Instance[key];
        return label != key ? label : resource;
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        foreach (var seg in _segments)
            if (!string.IsNullOrWhiteSpace(seg.Raw))
                _panel.Children.Add(CreateBadge(seg));
        _addBtn.FontSize = FontScale.Of(14);
        ToolTip.SetTip(_addBtn, Loc.Instance["TipAddCost"]);
        _panel.Children.Add(_addBtn);
    }

    private Control CreateBadge(Segment seg)
    {
        var known = seg.Resource.Length > 0;
        var color = known && ResourceColors.TryGetValue(seg.Resource, out var c) ? c : OtherColor;
        var brush = new SolidColorBrush(color);
        var row = new WrapPanel { Orientation = Orientation.Horizontal };

        var label = new TextBlock
        {
            Text = known ? ResourceLabel(seg.Resource) : seg.Raw.Trim(),
            FontSize = FontScale.Of(11), FontWeight = FontWeight.SemiBold,
            Foreground = known ? brush : ThemeBrushes.TextMuted,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        // Vanilla spells never spend WarlockSpellSlot directly: SpellSlotsGroup is the pool of regular
        // and pact slots, so the slot badge says so instead of offering a separate warlock resource.
        ToolTip.SetTip(label, seg.IsSlot ? $"{Loc.Instance["TipSpellSlotsGroup"]}\n{seg.Raw.Trim()}" : seg.Raw.Trim());
        row.Children.Add(label);

        if (seg.IsSlot)
        {
            row.Children.Add(Caption(Loc.Instance.LblSpellLevel));
            row.Children.Add(NumberChip(seg.Level!.Value, 1, 9, v => seg.Level = v));
            row.Children.Add(Caption("×"));
            row.Children.Add(NumberChip(seg.Amount!.Value, 1, 9, v => seg.Amount = v));
        }
        else if (seg.Amount != null)
            row.Children.Add(NumberChip(seg.Amount.Value, 0, 99, v => seg.Amount = v));
        else if (seg.Value != null)
            row.Children.Add(Caption(seg.Value));

        var remove = new Button
        {
            Content = "×", FontSize = FontScale.Of(11),
            Padding = new Thickness(4, 0),
            Background = Brushes.Transparent, Foreground = ThemeBrushes.TextMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        remove.Click += (_, _) =>
        {
            _segments.Remove(seg);
            WriteText();
            Rebuild();
        };
        row.Children.Add(remove);
        foreach (var child in row.Children)
            if (child.Margin == default) child.Margin = new Thickness(0, 1, 4, 1);

        // A Panel rather than a Border with a child, so the tumbler drum is not clipped.
        return new Panel
        {
            // Centred in its row: beside a badge with tumblers a plain one would stretch to its height.
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 6, 2),
            ClipToBounds = false,
            Children =
            {
                new Border
                {
                    Background = new SolidColorBrush(color, 0.15), BorderBrush = brush,
                    BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                    IsHitTestVisible = false,
                },
                new Border { Child = row, Padding = new Thickness(8, 2), ClipToBounds = false },
            },
        };
    }

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = FontScale.Of(10),
        Foreground = ThemeBrushes.TextSecondary,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private TumblerChipEditor NumberChip(int value, int min, int max, Action<int> set)
    {
        var chip = new TumblerChipEditor
        {
            Text = value.ToString(), Step = 1, MinValue = min, MaxValue = max,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Subscribed after the initial Text, so building a badge never writes back.
        chip.PropertyChanged += (_, e) =>
        {
            if (e.Property != TumblerChipEditor.TextProperty || !int.TryParse(chip.Text, out var v)) return;
            set(v);
            WriteText();
        };
        return chip;
    }

    private void OpenAddMenu()
    {
        var menu = new ContextMenu();
        var present = new HashSet<string>(_segments.Select(s => s.Resource), StringComparer.OrdinalIgnoreCase);
        foreach (var resource in CostResources.Where(r => !present.Contains(r)))
        {
            var item = new MenuItem { Header = ResourceLabel(resource), Foreground = new SolidColorBrush(ResourceColors[resource]) };
            item.Click += (_, _) => Add(resource);
            menu.Items.Add(item);
        }
        if (menu.Items.Count > 0) menu.Open(_addBtn);
    }

    private void Add(string resource)
    {
        _segments.RemoveAll(s => string.IsNullOrWhiteSpace(s.Raw));
        _segments.Add(ParseSegment(resource == SpellSlots ? $"{SpellSlots}:1:1:1" : $"{resource}:1"));
        WriteText();
        Rebuild();
    }
}
