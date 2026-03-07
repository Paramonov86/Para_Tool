namespace ParaTool.Core.Models;

public sealed class ProfileData
{
    public Dictionary<string, ModSelection> Mods { get; set; } = new();
}

public sealed class ModSelection
{
    public string ModName { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, ItemSettings> Items { get; set; } = new();
}

public sealed class ItemSettings
{
    public bool Enabled { get; set; } = true;
    public string? Pool { get; set; }
    public string? Rarity { get; set; }
    public List<string> Themes { get; set; } = new();
}

public sealed class ApplyResult
{
    public int RestoredCount { get; init; }
    public List<MissingItem> MissingItems { get; init; } = new();
}

public sealed class MissingItem
{
    public required string ModName { get; init; }
    public required string StatId { get; init; }
}
