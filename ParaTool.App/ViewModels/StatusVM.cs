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
