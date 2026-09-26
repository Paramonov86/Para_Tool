using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.App.Localization;
using ParaTool.App.Services;
using ParaTool.Core.Artifacts;
using ParaTool.Core.Schema;

namespace ParaTool.App.ViewModels;

/// <summary>
/// Wraps a status card. A status is simpler than a spell: text, an icon, how it stacks and ticks,
/// what it gives while active and what it does when applied, each turn and when removed. Visuals
/// and sounds stay inherited from the status it was copied from.
/// </summary>
public partial class StatusVM : ObservableObject
{
    public StatusDefinition Status { get; }
    private readonly ArtifactItemVM _parent;

    private string EditLang => _parent.GetEditingLang?.Invoke() ?? Loc.Instance.Lang;

    private static string[] ValueList(string name, params string[] skip) =>
        (StatsSchema.Instance.GetValueList(name)?.Values ?? [])
            .Where(v => !skip.Contains(v, StringComparer.Ordinal)).ToArray();

    /// <summary>Types a status made from scratch can have; a copy keeps its original's.</summary>
    public static string[] TypeOptions { get; } = ["BOOST", "EFFECT", "INCAPACITATED", "INVISIBLE", "FEAR", "KNOCKED_DOWN"];
    public static string[] StackTypeOptions { get; } = ValueList("StatusStackType");
    public static string[] TickTypeOptions { get; } = ValueList("TickType");
    public static string[] FlagOptions { get; } = ValueList("StatusPropertyFlags", "None");
    public static string[] GroupOptions { get; } = ValueList("StatusGroupFlags", "SG_None");
    public static string[] RemoveEventOptions { get; } = ValueList("StatusEvent", "None", "UNUSED1");

    public static string[] TypeLabels => Loc.Instance.ValueLabels("StatusType", TypeOptions);
    public static string[] StackTypeLabels => Loc.Instance.ValueLabels("StatusStackType", StackTypeOptions);
    public static string[] TickTypeLabels => Loc.Instance.ValueLabels("TickType", TickTypeOptions);
    public static string[] FlagLabels => Loc.Instance.ValueLabels("StatusPropertyFlags", FlagOptions);
    public static string[] GroupLabels => Loc.Instance.ValueLabels("StatusGroupFlags", GroupOptions);
    public static string[] RemoveEventLabels => Loc.Instance.ValueLabels("StatusEvent", RemoveEventOptions);

    public StatusVM(StatusDefinition status, ArtifactItemVM parent)
    {
        Status = status;
        _parent = parent;
    }

    [ObservableProperty] private bool _isExpanded;

    /// <summary>The appearance block (colour, effects, sounds, animations) is open.</summary>
    [ObservableProperty] private bool _isAppearanceExpanded;

    private IReadOnlyList<AppearanceGroupVM>? _appearance;

    /// <summary>How the status looks and sounds, in three groups.</summary>
    public IReadOnlyList<AppearanceGroupVM> Appearance => _appearance ??=
    [
        new(Loc.Instance.LblStatusLook, [Field("FormatColor"), Field("StatusEffect"), Field("ApplyEffect")]),
        new(Loc.Instance.LblStatusSound, [Field("SoundStart"), Field("SoundLoop"), Field("SoundStop"),
                                          Field("SoundVocalStart"), Field("SoundVocalLoop"), Field("SoundVocalEnd")]),
        new(Loc.Instance.LblStatusAnimation, [Field("AnimationStart"), Field("AnimationLoop"), Field("AnimationEnd"),
                                              Field("StillAnimationType"), Field("StillAnimationPriority")]),
    ];

    private AppearanceFieldVM Field(string key) => new(this, key);

    internal void RecordAppearanceEdit(string key) => _parent.RecordEdit("Appearance." + key);

    /// <summary>What the status the card was copied from gives a field (through its whole using chain).</summary>
    internal string? InheritedValue(string key)
    {
        if (IsBlank || Controls.BoostBlocksEditor.GlobalResolver is not { } resolver) return null;
        return resolver.ResolveAll(Status.UsingBase!).GetValueOrDefault(key);
    }

    /// <summary>Made from scratch — no original to inherit from or to edit.</summary>
    public bool IsBlank => Status.UsingBase == null;
    public bool IsCopy => !IsBlank;

    public string Name
    {
        get
        {
            var lang = EditLang;
            if (Status.DisplayName.TryGetValue(lang, out var n) && !string.IsNullOrEmpty(n)) return n;
            if (Status.DisplayName.TryGetValue("en", out var en) && !string.IsNullOrEmpty(en)) return en;
            return Status.UsingBase ?? Status.Name;
        }
    }

    /// <summary>The status the card was copied from, or the card's own name (tooltip).</summary>
    public string StatName => Status.UsingBase ?? Status.Name;
    public string TypeLabel => Loc.Instance.ValueLabel("StatusType", Status.StatusType);

    public bool EditOriginal
    {
        get => Status.EditOriginal;
        set
        {
            if (Status.EditOriginal == value || IsBlank) return;
            Status.EditOriginal = value;
            _parent.RecordEdit();
            OnPropertyChanged();
        }
    }

    /// <summary>Listed in the item's StatusOnEquip: whoever wears the item has it.</summary>
    public bool ApplyOnEquip
    {
        get => _parent.IsStatusAppliedOnEquip(Status);
        set
        {
            if (ApplyOnEquip == value) return;
            _parent.SetStatusAppliedOnEquip(this, value);
        }
    }

    internal void NotifyApplyChanged() => OnPropertyChanged(nameof(ApplyOnEquip));

    public string EditDisplayName
    {
        get => GetLang(Status.DisplayName);
        set
        {
            if (GetLang(Status.DisplayName) == (value ?? "")) return;
            Status.DisplayName[EditLang] = value ?? "";
            Status.DisplayNameEdited = true;
            _parent.RecordEdit();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Name));
        }
    }

    public string EditDescription
    {
        get => GetLang(Status.Description);
        set
        {
            if (GetLang(Status.Description) == (value ?? "")) return;
            Status.Description[EditLang] = value ?? "";
            Status.DescriptionEdited = true;
            _parent.RecordEdit();
            OnPropertyChanged();
        }
    }

    public string EditStatusType
    {
        get => Status.StatusType;
        set
        {
            if (!IsBlank || string.IsNullOrEmpty(value) || Status.StatusType == value) return;
            Status.StatusType = value;
            _parent.RecordEdit();
            OnPropertyChanged();
            OnPropertyChanged(nameof(TypeLabel));
        }
    }

    // ── Icon ──

    public string EditIcon => Status.Icon ?? "";
    public bool HasIcon => IconBitmap != null;
    public WriteableBitmap? IconBitmap => IconLibraryService.Current?.Thumb(Status.Icon);

    internal void SetIcon(string? icon)
    {
        icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
        if (Status.Icon == icon) return;
        Status.Icon = icon;
        _parent.RecordEdit();
        RefreshIcon();
    }

    internal void RefreshIcon()
    {
        OnPropertyChanged(nameof(EditIcon));
        OnPropertyChanged(nameof(IconBitmap));
        OnPropertyChanged(nameof(HasIcon));
    }

    // ── Fields. A value equal to what is shown is not written back: an editor redrawing on load
    // must not turn a field the card never had (null, inherited) into an empty one. ──

    private bool Set(string? current, string? value, Action<string> assign, [System.Runtime.CompilerServices.CallerMemberName] string? prop = null)
    {
        if ((current ?? "") == (value ?? "")) return false;
        assign(value ?? "");
        _parent.RecordEdit(prop);
        OnPropertyChanged(prop);
        return true;
    }

    public string EditDescriptionParams { get => Status.DescriptionParams; set => Set(Status.DescriptionParams, value, v => Status.DescriptionParams = v); }
    public string EditStackId { get => Status.StackId ?? ""; set => Set(Status.StackId, value, v => Status.StackId = v.Trim()); }
    public string EditStackType { get => Status.StackType ?? ""; set => Set(Status.StackType, value, v => Status.StackType = v); }
    public string EditTickType { get => Status.TickType ?? ""; set => Set(Status.TickType, value, v => Status.TickType = v); }
    public string EditStatusPropertyFlags { get => Status.StatusPropertyFlags; set => Set(Status.StatusPropertyFlags, value, v => Status.StatusPropertyFlags = v); }
    public string EditStatusGroups { get => Status.StatusGroups; set => Set(Status.StatusGroups, value, v => Status.StatusGroups = v); }
    public string EditRemoveEvents { get => Status.RemoveEvents; set => Set(Status.RemoveEvents, value, v => Status.RemoveEvents = v); }
    public string EditBoosts { get => Status.Boosts; set => Set(Status.Boosts, value, v => Status.Boosts = v); }
    public string EditPassives { get => Status.Passives ?? Status.PassivesOnApply; set => Set(Status.Passives ?? Status.PassivesOnApply, value, v => Status.Passives = v); }
    public string EditOnApplyFunctors { get => Status.OnApplyFunctors ?? ""; set => Set(Status.OnApplyFunctors, value, v => Status.OnApplyFunctors = v); }
    public string EditTickFunctors { get => Status.TickFunctors ?? ""; set => Set(Status.TickFunctors, value, v => Status.TickFunctors = v); }
    public string EditOnRemoveFunctors { get => Status.OnRemoveFunctors ?? ""; set => Set(Status.OnRemoveFunctors, value, v => Status.OnRemoveFunctors = v); }
    public string EditRemoveConditions { get => Status.RemoveConditions ?? ""; set => Set(Status.RemoveConditions, value, v => Status.RemoveConditions = v); }
    public string EditAuraRadius { get => Status.AuraRadius ?? ""; set => Set(Status.AuraRadius, value, v => Status.AuraRadius = v.Trim()); }
    public string EditAuraStatuses { get => Status.AuraStatuses ?? ""; set => Set(Status.AuraStatuses, value, v => Status.AuraStatuses = v); }

    public string EditStackPriority
    {
        get => Status.StackPriority?.ToString() ?? "";
        set
        {
            var v = value?.Trim() ?? "";
            int? parsed = int.TryParse(v, out var n) ? n : null;
            if (v.Length > 0 && parsed == null) return;
            if (parsed == Status.StackPriority) return;
            Status.StackPriority = parsed;
            _parent.RecordEdit();
            OnPropertyChanged();
        }
    }

    private string GetLang(Dictionary<string, string> dict)
    {
        var lang = EditLang;
        if (dict.TryGetValue(lang, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (dict.TryGetValue("en", out var en) && !string.IsNullOrEmpty(en)) return en;
        return "";
    }
}

/// <summary>One group of a status card's appearance settings.</summary>
public sealed record AppearanceGroupVM(string Title, IReadOnlyList<AppearanceFieldVM> Fields);

/// <summary>
/// One appearance field of a status card: null leaves the original's, "" turns it off, anything
/// else sets it. Its choices depend on the field — the game's value lists, the visual effects of
/// the game and the mods, or the sounds and animations their stats use.
/// </summary>
public sealed class AppearanceFieldVM : ObservableObject
{
    private readonly StatusVM _card;
    public string Key { get; }

    public AppearanceFieldVM(StatusVM card, string key)
    {
        _card = card;
        Key = key;
    }

    public string Label => Loc.Instance["LblStatus" + Key];
    public string Tip => Loc.Instance["TipStatus" + Key];
    public bool AllowInherit => !_card.IsBlank;
    public bool FreeText => Key.StartsWith("Sound", StringComparison.Ordinal) && !Key.StartsWith("SoundVocal", StringComparison.Ordinal)
                            || Key.StartsWith("Animation", StringComparison.Ordinal);
    public string? Inherited => _card.InheritedValue(Key);

    public string? Text
    {
        get => _card.Status.GetAppearance(Key);
        set
        {
            if (_card.Status.GetAppearance(Key) == value) return;
            _card.Status.SetAppearance(Key, value);
            _card.RecordAppearanceEdit(Key);
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<Controls.PickOption> Options => AppearanceOptions.For(Key);
}

/// <summary>The choices of each appearance field, built once per language and effect library.</summary>
public static class AppearanceOptions
{
    private static readonly Dictionary<string, IReadOnlyList<Controls.PickOption>> Cache = new();
    private static string? _lang;
    private static object? _effects;

    private static readonly Dictionary<string, string> ValueListOf = new()
    {
        ["FormatColor"] = "FormatStringColor",
        ["SoundVocalStart"] = "SoundVocalType", ["SoundVocalLoop"] = "SoundVocalType", ["SoundVocalEnd"] = "SoundVocalType",
        ["StillAnimationType"] = "StatusAnimationType", ["StillAnimationPriority"] = "StillAnimPriority",
    };

    public static IReadOnlyList<Controls.PickOption> For(string key)
    {
        var effects = EffectLibraryService.Current;
        if (_lang != Loc.Instance.Lang || !ReferenceEquals(_effects, effects))
        {
            Cache.Clear();
            _lang = Loc.Instance.Lang;
            _effects = effects;
        }
        if (Cache.TryGetValue(key, out var cached)) return cached;
        return Cache[key] = Build(key, effects);
    }

    private static IReadOnlyList<Controls.PickOption> Build(string key, EffectLibraryService? effects)
    {
        if (ValueListOf.TryGetValue(key, out var list))
            return (StatsSchema.Instance.GetValueList(list)?.Values ?? [])
                .Where(v => v != "MAX")
                .Select(v => new Controls.PickOption(v, Loc.Instance.ValueLabel(list, v), v)).ToList();

        if (key is "StatusEffect" or "ApplyEffect")
        {
            if (effects == null) return [];
            // Effects some status uses come first: those are the ones made to sit on a character.
            return effects.Library.All
                .OrderBy(e => effects.StatusEffects.Contains(e.Uuid) ? 0 : 1)
                .ThenBy(e => e.Display, StringComparer.OrdinalIgnoreCase)
                .Select(e => new Controls.PickOption(e.Uuid, e.Display,
                    string.Join("  ·  ", new[] { e.Name, e.Source }.Concat(e.Files.Skip(1)).Distinct())))
                .ToList();
        }

        if (effects?.UsedValues.GetValueOrDefault(key) is { } used)
            return key.StartsWith("Animation", StringComparison.Ordinal)
                ? used.Select(v => new Controls.PickOption(v, EffectLibraryService.AnimationName(v), v)).ToList()
                : used.Select(v => new Controls.PickOption(v, v)).ToList();
        return [];
    }
}
