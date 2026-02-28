using System.Xml.Linq;
using ParaTool.Core.Models;

namespace ParaTool.Core.Patching;

public static class MetaLsxPatcher
{
    public static string Patch(string metaXml, IReadOnlyList<ModInfo> mods)
    {
        var doc = XDocument.Parse(metaXml);

        // Find Dependencies node (or ModuleInfo node to create it)
        var moduleInfo = doc.Descendants("node")
            .FirstOrDefault(n => n.Attribute("id")?.Value == "ModuleInfo");

        if (moduleInfo == null)
            return metaXml;

        var depsNode = moduleInfo.Elements("children").FirstOrDefault()
            ?.Elements("node").FirstOrDefault(n => n.Attribute("id")?.Value == "Dependencies")
            ?.Elements("children").FirstOrDefault();

        if (depsNode == null)
        {
            // Create Dependencies structure
            var children = moduleInfo.Element("children");
            if (children == null)
            {
                children = new XElement("children");
                moduleInfo.Add(children);
            }

            var depsParent = new XElement("node", new XAttribute("id", "Dependencies"),
                new XElement("children"));
            children.Add(depsParent);
            depsNode = depsParent.Element("children")!;
        }

        // Get existing UUIDs to avoid duplicates
        var existingUuids = depsNode.Elements("node")
            .Where(n => n.Attribute("id")?.Value == "ModuleShortDesc")
            .Select(n => n.Elements("attribute")
                .FirstOrDefault(a => a.Attribute("id")?.Value == "UUID")
                ?.Attribute("value")?.Value)
            .Where(v => v != null)
            .ToHashSet();

        foreach (var mod in mods)
        {
            if (existingUuids.Contains(mod.UUID)) continue;

            var shortDesc = new XElement("node", new XAttribute("id", "ModuleShortDesc"),
                new XElement("attribute",
                    new XAttribute("id", "Folder"),
                    new XAttribute("type", "LSString"),
                    new XAttribute("value", mod.Folder)),
                new XElement("attribute",
                    new XAttribute("id", "Name"),
                    new XAttribute("type", "LSString"),
                    new XAttribute("value", mod.Name)),
                new XElement("attribute",
                    new XAttribute("id", "UUID"),
                    new XAttribute("type", "FixedString"),
                    new XAttribute("value", mod.UUID)),
                new XElement("attribute",
                    new XAttribute("id", "Version64"),
                    new XAttribute("type", "int64"),
                    new XAttribute("value", "36028797018963968"))
            );

            depsNode.Add(shortDesc);
        }

        return doc.Declaration != null
            ? doc.Declaration.ToString() + "\n" + doc.ToString()
            : doc.ToString();
    }
}
