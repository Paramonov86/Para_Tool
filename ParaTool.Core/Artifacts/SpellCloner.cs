using ParaTool.Core.Localization;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Artifacts;

/// <summary>
/// Builds a spell card from an existing SpellData entry. Only the fields the card edits are
/// copied; everything else (animation, VFX, tooltips, …) stays inherited through <c>using</c>.
/// Text is not resolved here — the caller fills DisplayName/Description from loca.
/// </summary>
public static class SpellCloner
{
    public static SpellDefinition CloneFrom(string spellName, StatsResolver? resolver)
    {
        var (nameHandle, descHandle) = HandleGenerator.NewPair();
        var spell = new SpellDefinition
        {
            Name = spellName,
            UsingBase = spellName,
            DisplayNameHandle = nameHandle,
            DescriptionHandle = descHandle,
        };
        if (resolver == null) return spell;

        var f = resolver.ResolveAll(spellName);
        string? Get(string key) => f.TryGetValue(key, out var v) ? v : null;

        spell.SpellType = Get("SpellType") ?? spell.SpellType;
        spell.DescriptionParams = Get("DescriptionParams") ?? "";
        spell.Icon = Get("Icon");
        spell.SpellProperties = Get("SpellProperties") ?? "";
        spell.UseCosts = Get("UseCosts") ?? "";
        spell.Cooldown = Get("Cooldown") ?? "";
        spell.TargetConditions = Get("TargetConditions") ?? "";
        spell.SpellFlags = Get("SpellFlags") ?? "";
        spell.Level = Get("Level");
        spell.SpellSchool = Get("SpellSchool");
        spell.TargetRadius = Get("TargetRadius");
        spell.AreaRadius = Get("AreaRadius");
        spell.SpellRoll = Get("SpellRoll");
        spell.SpellSuccess = Get("SpellSuccess");
        spell.SpellFail = Get("SpellFail");

        if (Get("DisplayName") is { Length: > 0 } dn) spell.SourceDisplayNameHandle = HandleGenerator.Parse(dn).handle;
        if (Get("Description") is { Length: > 0 } dd) spell.SourceDescriptionHandle = HandleGenerator.Parse(dd).handle;
        return spell;
    }

    /// <summary>
    /// A card for the spell, plus a card for each variant when the spell is a container, so the
    /// effects that live on the variants can be edited and copied with it.
    /// </summary>
    public static SpellDefinition CloneWithVariants(string spellName, StatsResolver? resolver)
    {
        var spell = CloneFrom(spellName, resolver);
        if (resolver != null)
            foreach (var variant in ListedVariants(spellName, resolver))
                spell.Variants.Add(CloneFrom(variant, resolver));
        return spell;
    }

    /// <summary>
    /// The variants a container spell lists, in order; empty when the spell is not a container. A
    /// container resolves a <c>ContainerSpells</c> list and no <c>SpellContainerID</c> of its own —
    /// variants that <c>using</c> their container inherit its list too, so the list alone is not enough.
    /// </summary>
    public static IReadOnlyList<string> ListedVariants(string spellName, StatsResolver resolver)
    {
        var fields = resolver.ResolveAll(spellName);
        if (!string.IsNullOrEmpty(fields.GetValueOrDefault("SpellContainerID"))) return [];
        return (fields.GetValueOrDefault("ContainerSpells") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(v => !v.Equals(spellName, StringComparison.OrdinalIgnoreCase) && resolver.Get(v) != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Card fields and the stats keys they compile to, for tests and diagnostics.</summary>
    public static readonly string[] CardFields =
    [
        "DescriptionParams", "Icon", "Level", "SpellSchool", "UseCosts", "Cooldown", "TargetRadius",
        "AreaRadius", "SpellRoll", "SpellSuccess", "SpellFail", "SpellProperties", "TargetConditions", "SpellFlags",
    ];
}
