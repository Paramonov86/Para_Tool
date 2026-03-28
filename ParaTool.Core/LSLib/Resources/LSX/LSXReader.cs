using System.Xml;

namespace ParaTool.Core.LSLib;

public class LSXReader(Stream stream) : IDisposable
{
    private readonly Stream stream = stream;
    private readonly Stack<Node> nodeStack = new();
    private Region? currentRegion;
    private Resource? resource;
    private LSXVersion version = LSXVersion.V4;

    public void Dispose() => stream.Dispose();

    public Resource Read()
    {
        resource = new Resource();

        using var reader = XmlReader.Create(stream);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
                ReadElement(reader);
            else if (reader.NodeType == XmlNodeType.EndElement)
                ReadEndElement(reader);
        }

        return resource;
    }

    private void ReadElement(XmlReader reader)
    {
        bool isEmpty = reader.IsEmptyElement;

        switch (reader.Name)
        {
            case "save":
                break;

            case "version":
                {
                    var major = reader.GetAttribute("major");
                    var minor = reader.GetAttribute("minor");
                    var revision = reader.GetAttribute("revision");
                    var build = reader.GetAttribute("build");

                    resource!.Metadata.MajorVersion = major != null ? uint.Parse(major) : 0;
                    resource.Metadata.MinorVersion = minor != null ? uint.Parse(minor) : 0;
                    resource.Metadata.Revision = revision != null ? uint.Parse(revision) : 0;
                    resource.Metadata.BuildNumber = build != null ? uint.Parse(build) : 0;

                    // Detect version from metadata
                    if (resource.Metadata.MajorVersion >= 4)
                        version = LSXVersion.V4;

                    var meta = reader.GetAttribute("lslib_meta");
                    if (meta != null)
                    {
                        var settings = new NodeSerializationSettings();
                        settings.InitFromMeta(meta);
                        resource.MetadataFormat = settings.LSFMetadata;
                    }
                    break;
                }

            case "region":
                {
                    var id = reader.GetAttribute("id") ?? "";
                    var region = new Region { Name = id, RegionName = id };
                    currentRegion = region;
                    resource!.Regions[id] = region;
                    break;
                }

            case "node":
                {
                    var id = reader.GetAttribute("id") ?? "";
                    var key = reader.GetAttribute("key");

                    if (nodeStack.Count == 0 && currentRegion != null)
                    {
                        // First node in region — it IS the region node
                        currentRegion.Name = id;
                        currentRegion.KeyAttribute = key;
                        nodeStack.Push(currentRegion);
                    }
                    else
                    {
                        var node = new Node { Name = id, KeyAttribute = key };
                        if (nodeStack.Count > 0)
                        {
                            node.Parent = nodeStack.Peek();
                            nodeStack.Peek().AppendChild(node);
                        }
                        nodeStack.Push(node);
                    }

                    if (isEmpty)
                        ReadEndElement_Node();
                    break;
                }

            case "attribute":
                ReadAttribute(reader);
                break;

            case "children":
                // Just a container, nothing to do
                break;
        }
    }

    private void ReadAttribute(XmlReader reader)
    {
        if (nodeStack.Count == 0) return;

        var id = reader.GetAttribute("id") ?? "";
        var typeStr = reader.GetAttribute("type") ?? "";

        // Resolve type: V4 uses names, V3 uses numeric IDs
        AttributeType attrType;
        if (version >= LSXVersion.V4 || !int.TryParse(typeStr, out _))
        {
            if (!AttributeTypeMaps.TypeToId.TryGetValue(typeStr, out attrType))
                attrType = AttributeType.None;
        }
        else
        {
            attrType = (AttributeType)int.Parse(typeStr);
        }

        var node = nodeStack.Peek();

        if (attrType == AttributeType.TranslatedString)
        {
            var ts = new TranslatedString();
            ts.Handle = reader.GetAttribute("handle") ?? "";
            var versionStr = reader.GetAttribute("version");
            if (versionStr != null)
                ts.Version = ushort.Parse(versionStr);
            var value = reader.GetAttribute("value");
            if (value != null)
                ts.Value = value;
            node.Attributes[id] = new NodeAttribute(attrType) { Value = ts };
        }
        else if (attrType == AttributeType.TranslatedFSString)
        {
            var fs = new TranslatedFSString
            {
                Value = reader.GetAttribute("value") ?? "",
                Handle = reader.GetAttribute("handle") ?? "",
                Arguments = new List<TranslatedFSStringArgument>()
            };
            var argCount = reader.GetAttribute("arguments");
            // Arguments are parsed as child elements if present
            node.Attributes[id] = new NodeAttribute(attrType) { Value = fs };
        }
        else
        {
            var value = reader.GetAttribute("value") ?? "";
            // ByteSwapGuids default for BG3
            bool byteSwap = resource!.Metadata.MajorVersion >= 4;
            var parsed = NodeAttribute.ParseFromString(value, attrType, byteSwap);
            node.Attributes[id] = new NodeAttribute(attrType) { Value = parsed };
        }
    }

    private void ReadEndElement(XmlReader reader)
    {
        switch (reader.Name)
        {
            case "node":
                ReadEndElement_Node();
                break;
            case "region":
                currentRegion = null;
                break;
        }
    }

    private void ReadEndElement_Node()
    {
        if (nodeStack.Count > 0)
            nodeStack.Pop();
    }
}
