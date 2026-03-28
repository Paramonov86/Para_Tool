using System.Xml;

namespace ParaTool.Core.LSLib;

public class LSXWriter(Stream stream)
{
    private readonly Stream stream = stream;
    private XmlWriter writer = null!;

    public bool PrettyPrint = false;
    public LSXVersion Version = LSXVersion.V3;
    public NodeSerializationSettings SerializationSettings = new();

    public void Write(Resource rsrc)
    {
        var settings = new XmlWriterSettings { Indent = PrettyPrint, IndentChars = "\t" };
        using (writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartElement("save");
            writer.WriteStartElement("version");
            writer.WriteAttributeString("major", rsrc.Metadata.MajorVersion.ToString());
            writer.WriteAttributeString("minor", rsrc.Metadata.MinorVersion.ToString());
            writer.WriteAttributeString("revision", rsrc.Metadata.Revision.ToString());
            writer.WriteAttributeString("build", rsrc.Metadata.BuildNumber.ToString());
            writer.WriteAttributeString("lslib_meta", SerializationSettings.BuildMeta());
            writer.WriteEndElement();
            WriteRegions(rsrc);
            writer.WriteEndElement();
        }
    }

    private void WriteRegions(Resource rsrc)
    {
        foreach (var region in rsrc.Regions)
        {
            writer.WriteStartElement("region");
            writer.WriteAttributeString("id", region.Key);
            WriteNode(region.Value);
            writer.WriteEndElement();
        }
    }

    private void WriteTranslatedFSStringInner(TranslatedFSString fs)
    {
        writer.WriteAttributeString("handle", fs.Handle);
        writer.WriteAttributeString("arguments", fs.Arguments.Count.ToString());
        if (fs.Arguments.Count > 0)
        {
            writer.WriteStartElement("arguments");
            foreach (var argument in fs.Arguments)
            {
                writer.WriteStartElement("argument");
                writer.WriteAttributeString("key", argument.Key);
                writer.WriteAttributeString("value", argument.Value);
                writer.WriteStartElement("string");
                writer.WriteAttributeString("value", argument.String.Value);
                WriteTranslatedFSStringInner(argument.String);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
    }

    private void WriteNode(Node node)
    {
        writer.WriteStartElement("node");
        writer.WriteAttributeString("id", node.Name);
        if (node.KeyAttribute != null)
            writer.WriteAttributeString("key", node.KeyAttribute);

        foreach (var attribute in node.Attributes)
        {
            writer.WriteStartElement("attribute");
            writer.WriteAttributeString("id", attribute.Key);
            writer.WriteAttributeString("type",
                Version >= LSXVersion.V4
                    ? AttributeTypeMaps.IdToType[attribute.Value.Type]
                    : ((int)attribute.Value.Type).ToString());

            if (attribute.Value.Type == AttributeType.TranslatedString)
            {
                var ts = (TranslatedString)attribute.Value.Value;
                writer.WriteAttributeString("handle", ts.Handle);
                if (ts.Value != null)
                    writer.WriteAttributeString("value", ts.ToString());
                else
                    writer.WriteAttributeString("version", ts.Version.ToString());
            }
            else if (attribute.Value.Type == AttributeType.TranslatedFSString)
            {
                var fs = (TranslatedFSString)attribute.Value.Value;
                writer.WriteAttributeString("value", fs.Value);
                WriteTranslatedFSStringInner(fs);
            }
            else
            {
                writer.WriteAttributeString("value",
                    attribute.Value.AsString(SerializationSettings).Replace("\x1f", ""));
            }
            writer.WriteEndElement();
        }

        if (node.ChildCount > 0)
        {
            writer.WriteStartElement("children");
            foreach (var children in node.Children)
                foreach (var child in children.Value)
                    WriteNode(child);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }
}
