using System.Xml.Linq;
using ParaTool.Core.Models;

namespace ParaTool.Core.Patching;

public static class MetaLsxPatcher
{
    public static string Patch(string metaXml, IReadOnlyList<ModInfo> mods)
    {
        var doc = XDocument.Parse(metaXml);

        // Structure: save > region > node(root) > children > node(Dependencies) > children
        // Dependencies is a SIBLING of ModuleInfo, both under node(root)/children
        var rootNode = doc.Descendants("node")
            .FirstOrDefault(n => n.Attribute("id")?.Value == "root");

        if (rootNode == null)
            return metaXml;

        var rootChildren = rootNode.Element("children");
        if (rootChildren == null)
            return metaXml;

        var depsParent = rootChildren
            .Elements("node")
            .FirstOrDefault(n => n.Attribute("id")?.Value == "Dependencies");

        XElement depsChildren;

        if (depsParent == null)
        {
            // Create Dependencies node
            depsChildren = new XElement("children");
            depsParent = new XElement("node", new XAttribute("id", "Dependencies"), depsChildren);
            rootChildren.Add(depsParent);
        }
        else
        {
            depsChildren = depsParent.Element("children")!;
            if (depsChildren == null)
            {
                depsChildren = new XElement("children");
                depsParent.Add(depsChildren);
            }
        }

        // Get existing UUIDs to avoid duplicates
        var existingUuids = depsChildren.Elements("node")
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
                    new XAttribute("id", "MD5"),
                    new XAttribute("type", "LSString"),
                    new XAttribute("value", "")),
                new XElement("attribute",
                    new XAttribute("id", "Name"),
                    new XAttribute("type", "LSString"),
                    new XAttribute("value", mod.Name)),
                new XElement("attribute",
                    new XAttribute("id", "PublishHandle"),
                    new XAttribute("type", "uint64"),
                    new XAttribute("value", "0")),
                new XElement("attribute",
                    new XAttribute("id", "UUID"),
                    new XAttribute("type", "guid"),
                    new XAttribute("value", mod.UUID)),
                new XElement("attribute",
                    new XAttribute("id", "Version64"),
                    new XAttribute("type", "int64"),
                    new XAttribute("value", "36028797018963968"))
            );

            depsChildren.Add(shortDesc);
        }

        return doc.Declaration != null
            ? doc.Declaration.ToString() + "\n" + doc.ToString()
            : "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + doc.ToString();
    }
}
