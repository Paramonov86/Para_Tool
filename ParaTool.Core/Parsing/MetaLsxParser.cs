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

        if (name == null || uuid == null || folder == null)
            return null;

        return new ModInfo
        {
            Name = name,
            UUID = uuid,
            Folder = folder,
            PakPath = pakPath
        };
    }
}
