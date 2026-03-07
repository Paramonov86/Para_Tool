namespace ParaTool.Core.Models;

public sealed class ModInfo
{
    public required string Name { get; init; }
    public required string UUID { get; init; }
    public required string Folder { get; init; }
    public required string PakPath { get; init; }
    public string Version64 { get; init; } = "36028797018963968";
    public List<ItemEntry> Items { get; set; } = new();
}
