using System.Xml.Linq;
using ParaTool.Core.Models;

namespace ParaTool.Core.Parsing;

public static class MetaLsxParser
{
    public static ModInfo? Parse(byte[] xmlData, string pakPath)
    {
        using var ms = new MemoryStream(xmlData);
        var doc = XDocument.Load(ms);

        var moduleNode = doc.Descendants("node")
            .FirstOrDefault(n => n.Attribute("id")?.Value == "ModuleInfo");

        if (moduleNode == null)
            return null;

        string? GetAttrValue(string id) =>
            moduleNode.Elements("attribute")
                .FirstOrDefault(a => a.Attribute("id")?.Value == id)
                ?.Attribute("value")?.Value;

        var name = GetAttrValue("Name");
        var uuid = GetAttrValue("UUID");
        var folder = GetAttrValue("Folder");
        var version64 = GetAttrValue("Version64") ?? "36028797018963968";

        if (name == null || uuid == null || folder == null)
            return null;

        return new ModInfo
        {
            Name = name,
            UUID = uuid,
            Folder = folder,
            PakPath = pakPath,
            Version64 = version64,
            DependencyUuids = ParseDependencyUuids(doc)
        };
    }

    /// <summary>
    /// UUIDs listed under node(Dependencies) — the mods this pak declares it loads after.
    /// Used to detect AMP submods (paks that depend on AMP and patch it in place).
    /// </summary>
    private static HashSet<string> ParseDependencyUuids(XDocument doc)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var depsNode = doc.Descendants("node")
            .FirstOrDefault(n => n.Attribute("id")?.Value == "Dependencies");
        if (depsNode == null) return result;

        foreach (var shortDesc in depsNode.Elements("children")
            .Elements("node")
            .Where(n => n.Attribute("id")?.Value == "ModuleShortDesc"))
        {
            var depUuid = shortDesc.Elements("attribute")
                .FirstOrDefault(a => a.Attribute("id")?.Value == "UUID")
                ?.Attribute("value")?.Value;
            if (!string.IsNullOrEmpty(depUuid))
                result.Add(depUuid);
        }

        return result;
    }
}
