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

    // AMP item flag (native AMP items from AMP pak)
    public bool IsAmpItem { get; set; }

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

    public bool IsModified =>
        UserPool != null || UserRarity != null || UserThemes.Count > 0 || !Enabled;
}
