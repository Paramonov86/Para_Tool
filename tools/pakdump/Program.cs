using System.Text;
using ParaTool.Core;
using ParaTool.Core.LSLib;

var pak = @"C:\Users\user\AppData\Local\Larian Studios\Baldur's Gate 3\Mods\test_4b516620-9c56-aa18-96ae-a-dk21.pak";
using var fs = File.OpenRead(pak);
var header = PakReader.ReadHeader(fs);
var entries = PakReader.ReadFileList(fs, header);

// Scan all RootTemplates for Stats="WPN_Moonblade"
foreach (var e in entries)
{
    if (!e.Path.Contains("RootTemplates") || !e.Path.EndsWith(".lsf")) continue;
    try
    {
        var data = PakReader.ExtractFileData(fs, e);
        using var ms = new MemoryStream(data);
        var reader = new LSFReader(ms);
        var resource = reader.Read();

        foreach (var region in resource.Regions.Values)
            ScanNode(region, e.Path);
    }
    catch { }
}

// Also check loca
foreach (var e in entries)
{
    if (!e.Path.Contains("Localization")) continue;
    Console.WriteLine($"\nLOCA: {e.Path}");
    var data = PakReader.ExtractFileData(fs, e);
    var text = Encoding.UTF8.GetString(data);
    if (text.Contains("Moonblade", StringComparison.OrdinalIgnoreCase))
        Console.WriteLine("  >> Contains Moonblade!");
    Console.WriteLine($"  length={text.Length}");
}

void ScanNode(Node node, string path)
{
    string? mapKey = null, stats = null, displayName = null, icon = null;
    foreach (var attr in node.Attributes)
    {
        if (attr.Key.Equals("MapKey", StringComparison.OrdinalIgnoreCase))
            mapKey = attr.Value.Value?.ToString();
        else if (attr.Key.Equals("Stats", StringComparison.OrdinalIgnoreCase))
            stats = attr.Value.Value?.ToString();
        else if (attr.Key.Equals("DisplayName", StringComparison.OrdinalIgnoreCase))
            displayName = attr.Value.Value?.ToString();
        else if (attr.Key.Equals("Icon", StringComparison.OrdinalIgnoreCase))
            icon = attr.Value.Value?.ToString();
    }

    if (stats != null && stats.Contains("Moonblade", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"FOUND: {path}");
        Console.WriteLine($"  MapKey={mapKey}");
        Console.WriteLine($"  Stats={stats}");
        Console.WriteLine($"  DisplayName={displayName}");
        Console.WriteLine($"  Icon={icon}");
        // Print ALL attributes
        foreach (var attr in node.Attributes)
            Console.WriteLine($"  [{attr.Key}] = {attr.Value.Value} (type={attr.Value.Type})");
    }

    foreach (var childList in node.Children)
        foreach (var child in childList.Value)
            ScanNode(child, path);
}
