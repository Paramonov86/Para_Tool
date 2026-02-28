namespace ParaTool.Core.Parsing;

public sealed class StatsEntry
{
    public required string Name { get; init; }
    public required string Type { get; init; } // "Armor" or "Weapon"
    public string? Using { get; init; }
    public Dictionary<string, string> Data { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
