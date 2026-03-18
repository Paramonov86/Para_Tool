namespace ParaTool.Core.Models;

public sealed class OriginalTtMeta
{
    public int Version { get; set; }
    public string PakFileName { get; set; } = "";
    public long PakFileSize { get; set; }
}
