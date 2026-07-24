namespace ParaTool.Core.Models;

public sealed class ModInfo
{
    public required string Name { get; init; }
    public required string UUID { get; init; }
    public required string Folder { get; init; }
    public required string PakPath { get; init; }
    public string Version64 { get; init; } = "36028797018963968";
    public bool IsAmp { get; init; }
    public List<ItemEntry> Items { get; set; } = new();

    /// <summary>UUIDs this pak declares as dependencies in meta.lsx.</summary>
    public HashSet<string> DependencyUuids { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when the pak declares AMP as a dependency — it is a submod that loads AFTER AMP
    /// and rebalances it in place (e.g. Ancient Mega Pack Plus). Submods must never be written
    /// into AMP's own meta.lsx dependencies: that would form a dependency cycle
    /// (AMP → submod → AMP) and the game fails to load.
    /// </summary>
    public bool IsAmpSubmod { get; set; }

    /// <summary>StatIds of vanilla items that this mod overrides (for marking AMP items as modified).</summary>
    public List<string>? VanillaOverrides { get; set; }

    /// <summary>
    /// StatIds of AMP items this submod overrides. These are rebalances of existing AMP items,
    /// not new items — they must not be re-added to AMP loot tables or given a skeleton entry.
    /// </summary>
    public List<string>? AmpOverrides { get; set; }
}
