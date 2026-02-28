namespace ParaTool.Core.Models;

public sealed class ModInfo
{
    public required string Name { get; init; }
    public required string UUID { get; init; }
    public required string Folder { get; init; }
    public required string PakPath { get; init; }
    public List<ItemEntry> Items { get; set; } = new();
}
