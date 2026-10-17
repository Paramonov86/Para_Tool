namespace ParaTool.Core.Models;

public sealed class ItemEntry
{
    public required string StatId { get; init; }
    public required string StatType { get; init; } // "Armor" or "Weapon"

    // Resolved from stats chain
    public string? ResolvedSlot { get; set; }
    public string? ResolvedArmorType { get; set; }
    public string? ResolvedRarity { get; set; }
    public string? ResolvedShield { get; set; }
    public string? ResolvedWeaponProperties { get; set; }
    public string? ResolvedValueOverride { get; set; }
    public string? ResolvedUnique { get; set; }

    // Auto-detected from resolution
    public string? DetectedPool { get; set; }
    public string? DetectedRarity { get; set; }
    public List<string> DetectedThemes { get; set; } = new();

    // Localized display name (from RootTemplates + Localization)
    public string? DisplayName { get; set; }

    // Localized description / lore text (from RootTemplates + Localization)
    public string? Description { get; set; }

    // Icon name from RootTemplate (e.g. "Ring05", "AMP_Kroneth_Ring")
    public string? IconName { get; set; }

    // Loca handles for on-demand multi-language resolution
    public string? DisplayNameHandle { get; set; }
    public string? DescriptionHandle { get; set; }

    // StatId of the ancestor that provided the display name (for using-chain loca resolution)
    public string? LocaAncestorId { get; set; }

    // AMP item flag (native AMP items from AMP pak)
    public bool IsAmpItem { get; set; }

    /// <summary>
    /// An item of an AMP submod (AMP Plus): the submod places it in loot through its own
    /// TreasureTable — boss rewards stay boss rewards — so like AMP's own items it is left where
    /// it is unless the patcher list changes it.
    /// </summary>
    public bool HasOwnLoot { get; set; }

    // Item is already integrated into AMP loot tables (patched previously)
    public bool IsIntegrated { get; set; }

    // User-editable
    public bool Enabled { get; set; } = true;
    public string? UserPool { get; set; }
    public string? UserRarity { get; set; }
    public List<string> UserThemes { get; set; } = new();

    public string EffectivePool => UserPool ?? DetectedPool ?? "UNKNOWN";
    public string EffectiveRarity => UserRarity ?? DetectedRarity ?? "Uncommon";
    public List<string> EffectiveThemes => UserThemes.Count > 0 ? UserThemes : DetectedThemes;

    /// <summary>
    /// True when the patcher list changes the item. A user value equal to the detected one is no
    /// change: PATCH copies every item's themes into <see cref="UserThemes"/> (and saved sessions
    /// bring them back), which used to rewrite the loot placement and price of every item that
    /// has a theme.
    /// </summary>
    public bool IsModified =>
        (UserPool != null && UserPool != DetectedPool)
        || (UserRarity != null && UserRarity != DetectedRarity)
        || (UserThemes.Count > 0 && !UserThemes.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(DetectedThemes))
        || !Enabled;

    /// <summary>
    /// True when a saved artifact overrides this item's stats via the Constructor.
    /// </summary>
    public bool HasArtifactOverride { get; set; }

    /// <summary>
    /// Names of mods that override this item's stats (vanilla overrides from other mods).
    /// </summary>
    public List<string> ModifiedBy { get; set; } = new();

    /// <summary>
    /// Concatenated stats text for deep search (boosts, passives, statuses, descriptions, etc.)
    /// Built at scan time from resolved stats chain.
    /// </summary>
    public string? SearchableText { get; set; }
}
