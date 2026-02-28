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

    // User-editable
    public bool Enabled { get; set; } = true;
    public string? UserPool { get; set; }
    public string? UserRarity { get; set; }
    public List<string> UserThemes { get; set; } = new();

    public string EffectivePool => UserPool ?? DetectedPool ?? "UNKNOWN";
    public string EffectiveRarity => UserRarity ?? DetectedRarity ?? "Uncommon";
}
