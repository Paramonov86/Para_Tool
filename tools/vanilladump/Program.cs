// Dumps vanilla data ParaTool embeds but the game only ships inside its paks:
//   Vanilla_Characters.txt — every `type "Character"` stats entry (last pak wins)
//   Vanilla_SummonTemplates.tsv — templateUuid, Stats, DisplayName handle for every
//                                 character template a vanilla Summon() call spawns
// Usage: vanilladump <BG3 Data dir> <Vanilla_Spells.txt> <output dir>
using System.Text;
using System.Text.RegularExpressions;
using ParaTool.Core;
using ParaTool.Core.LSLib;

if (args.Length < 3)
{
    Console.Error.WriteLine("usage: vanilladump <BG3 Data dir> <Vanilla_Spells.txt> <output dir>");
    return 1;
}
var dataDir = args[0];
var spellsPath = args[1];
var outDir = args[2];
Directory.CreateDirectory(outDir);

var patchPak = Directory.GetFiles(dataDir, "Patch*.pak")
    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).LastOrDefault();
var paks = new[] { "Shared.pak", "Gustav.pak", "GustavX.pak" }
    .Select(p => Path.Combine(dataDir, p))
    .Concat(patchPak != null ? [patchPak] : [])
    .Where(File.Exists)
    .ToList();

var characters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var characterOrder = new List<string>();
// template uuid -> (type, stats, parent, displayName handle); later paks override
var templates = new Dictionary<string, (string? type, string? stats, string? parent, string? name)>(StringComparer.OrdinalIgnoreCase);

foreach (var pak in paks)
{
    Console.WriteLine($"Reading {Path.GetFileName(pak)}");
    using var fs = File.OpenRead(pak);
    var header = PakReader.ReadHeader(fs);
    var entries = PakReader.ReadFileList(fs, header);

    foreach (var e in entries)
    {
        var path = e.Path.Replace('\\', '/');
        if (path.Contains("/Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            var text = Encoding.UTF8.GetString(PakReader.ExtractFileData(fs, e)).Replace("\r\n", "\n");
            foreach (var block in SplitEntries(text))
            {
                if (!block.Contains("\ntype \"Character\"")) continue;
                var name = Regex.Match(block, "^new entry \"([^\"]+)\"").Groups[1].Value;
                if (!characters.ContainsKey(name)) characterOrder.Add(name);
                characters[name] = block;
            }
        }
        else if (path.Contains("/RootTemplates/", StringComparison.OrdinalIgnoreCase)
                 && path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var ms = new MemoryStream(PakReader.ExtractFileData(fs, e));
                var resource = new LSFReader(ms).Read();
                foreach (var region in resource.Regions.Values)
                    CollectTemplates(region);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  skip {path}: {ex.Message}");
            }
        }
    }
}

// ── Characters ──
var sb = new StringBuilder();
foreach (var name in characterOrder)
    sb.Append(characters[name].TrimEnd('\n')).Append("\n\n");
File.WriteAllText(Path.Combine(outDir, "Vanilla_Characters.txt"), sb.ToString().Replace("\n", "\r\n"));
Console.WriteLine($"Characters: {characterOrder.Count}");

// ── Summon templates ──
var spells = File.ReadAllText(spellsPath);
var summonUuids = Regex.Matches(spells, @"\bSummon\(\s*([0-9a-fA-F-]{36})")
    .Select(m => m.Groups[1].Value.ToLowerInvariant())
    .Distinct()
    .OrderBy(u => u, StringComparer.Ordinal)
    .ToList();

var names = new Dictionary<string, Dictionary<string, string>>
{
    ["en"] = LoadLoca(Path.Combine(dataDir, "Localization", "English.pak")),
    ["ru"] = LoadLoca(Path.Combine(dataDir, "Localization", "Russian", "Russian.pak")),
};
string NameText(string lang, string? handle) =>
    handle != null && names[lang].TryGetValue(handle, out var t)
        ? t.Replace('\t', ' ').Replace('\n', ' ').Replace("\r", "") : "";

var tsv = new StringBuilder("templateUuid\tstats\tdisplayNameHandle\tname_en\tname_ru\n");
int resolved = 0;
foreach (var uuid in summonUuids)
{
    string? stats = null, nameHandle = null, type = null;
    var cur = uuid;
    for (int depth = 0; cur != null && depth < 20 && templates.TryGetValue(cur, out var t); depth++)
    {
        type ??= t.type;
        stats ??= t.stats;
        nameHandle ??= t.name;
        cur = string.IsNullOrEmpty(t.parent) ? null : t.parent;
    }
    if (type == null) { Console.Error.WriteLine($"  template not found {uuid}"); continue; }
    if (!type.Equals("character", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine($"  skip {type} {uuid} stats={stats}"); continue; }
    if (string.IsNullOrEmpty(stats)) { Console.Error.WriteLine($"  unresolved summon {uuid}"); continue; }
    tsv.Append(uuid).Append('\t').Append(stats).Append('\t').Append(nameHandle ?? "")
        .Append('\t').Append(NameText("en", nameHandle)).Append('\t').Append(NameText("ru", nameHandle)).Append('\n');
    resolved++;
}
File.WriteAllText(Path.Combine(outDir, "Vanilla_SummonTemplates.tsv"), tsv.ToString());
Console.WriteLine($"Summon templates: {resolved}/{summonUuids.Count}");
return 0;

static Dictionary<string, string> LoadLoca(string pakPath)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (!File.Exists(pakPath)) { Console.Error.WriteLine($"  no loca pak {pakPath}"); return map; }
    using var fs = File.OpenRead(pakPath);
    var entries = PakReader.ReadFileList(fs, PakReader.ReadHeader(fs));
    foreach (var e in entries.Where(e => e.Path.EndsWith(".loca", StringComparison.OrdinalIgnoreCase)))
    {
        using var ms = new MemoryStream(PakReader.ExtractFileData(fs, e));
        foreach (var t in LocaUtils.Load(ms, LocaFormat.Loca).Entries)
            map[t.Key] = t.Text;
    }
    Console.WriteLine($"Loca {Path.GetFileName(pakPath)}: {map.Count}");
    return map;
}

static IEnumerable<string> SplitEntries(string text)
{
    var starts = Regex.Matches(text, "^new entry ", RegexOptions.Multiline).Select(m => m.Index).ToList();
    for (int i = 0; i < starts.Count; i++)
    {
        var end = i + 1 < starts.Count ? starts[i + 1] : text.Length;
        yield return text[starts[i]..end];
    }
}

void CollectTemplates(Node node)
{
    string? mapKey = null, type = null, stats = null, parent = null, name = null;
    foreach (var (key, attr) in node.Attributes)
    {
        switch (key)
        {
            case "MapKey": mapKey = attr.Value?.ToString(); break;
            case "Type": type = attr.Value?.ToString(); break;
            case "Stats": stats = attr.Value?.ToString(); break;
            case "ParentTemplateId": parent = attr.Value?.ToString(); break;
            case "DisplayName": name = attr.Value is TranslatedString ts ? ts.Handle : attr.Value?.ToString(); break;
        }
    }
    if (mapKey != null && type != null)
        templates[mapKey] = (type, string.IsNullOrEmpty(stats) ? null : stats, parent, string.IsNullOrEmpty(name) ? null : name);
    foreach (var list in node.Children.Values)
        foreach (var child in list)
            CollectTemplates(child);
}
