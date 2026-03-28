using System.Text;

namespace ParaTool.Core.LSLib;

public class LSFWriter(Stream stream)
{
    private static readonly int StringHashMapSize = 0x200;

    private readonly Stream Stream = stream;
    private BinaryWriter Writer = null!;
    private LSMetadata Meta;

    private MemoryStream NodeStream = null!;
    private BinaryWriter NodeWriter = null!;
    private int NextNodeIndex = 0;
    private Dictionary<Node, int> NodeIndices = null!;

    private MemoryStream AttributeStream = null!;
    private BinaryWriter AttributeWriter = null!;
    private int NextAttributeIndex = 0;

    private MemoryStream ValueStream = null!;
    private BinaryWriter ValueWriter = null!;

    private MemoryStream KeyStream = null!;
    private BinaryWriter KeyWriter = null!;

    private List<List<string>> StringHashMap = null!;
    private List<int>? NextSiblingIndices;

    public LSFVersion Version = LSFVersion.MaxWriteVersion;
    public LSFMetadataFormat MetadataFormat = LSFMetadataFormat.None;
    public LSLibCompressionMethod Compression = LSLibCompressionMethod.None;
    public LSCompressionLevel CompressionLevel = LSCompressionLevel.Default;

    public void Write(Resource resource)
    {
        Meta = resource.Metadata;

        using (Writer = new BinaryWriter(Stream, Encoding.Default, true))
        using (NodeStream = new MemoryStream())
        using (NodeWriter = new BinaryWriter(NodeStream))
        using (AttributeStream = new MemoryStream())
        using (AttributeWriter = new BinaryWriter(AttributeStream))
        using (ValueStream = new MemoryStream())
        using (ValueWriter = new BinaryWriter(ValueStream))
        using (KeyStream = new MemoryStream())
        using (KeyWriter = new BinaryWriter(KeyStream))
        {
            NextNodeIndex = 0;
            NextAttributeIndex = 0;
            NodeIndices = [];
            NextSiblingIndices = null;
            StringHashMap = new List<List<string>>(StringHashMapSize);
            while (StringHashMap.Count < StringHashMapSize)
                StringHashMap.Add([]);

            if (MetadataFormat != LSFMetadataFormat.None)
                ComputeSiblingIndices(resource);

            WriteRegions(resource);

            byte[] stringBuffer;
            using (var stringStream = new MemoryStream())
            using (var stringWriter = new BinaryWriter(stringStream))
            {
                WriteStaticStrings(stringWriter);
                stringBuffer = stringStream.ToArray();
            }

            var nodeBuffer = NodeStream.ToArray();
            var attributeBuffer = AttributeStream.ToArray();
            var valueBuffer = ValueStream.ToArray();
            var keyBuffer = KeyStream.ToArray();

            var magic = new LSFMagic
            {
                Magic = BitConverter.ToUInt32(LSFMagic.Signature, 0),
                Version = (uint)Version
            };
            BinUtils.WriteStruct(Writer, ref magic);

            var gameVersion = new PackedVersion
            {
                Major = resource.Metadata.MajorVersion,
                Minor = resource.Metadata.MinorVersion,
                Revision = resource.Metadata.Revision,
                Build = resource.Metadata.BuildNumber
            };

            if (Version < LSFVersion.VerBG3ExtendedHeader)
            {
                var header = new LSFHeader { EngineVersion = gameVersion.ToVersion32() };
                BinUtils.WriteStruct(Writer, ref header);
            }
            else
            {
                var header = new LSFHeaderV5 { EngineVersion = gameVersion.ToVersion64() };
                BinUtils.WriteStruct(Writer, ref header);
            }

            bool chunked = Version >= LSFVersion.VerChunkedCompress;
            byte[] stringsCompressed = CompressionHelpers.Compress(stringBuffer, Compression, CompressionLevel);
            byte[] nodesCompressed = CompressionHelpers.Compress(nodeBuffer, Compression, CompressionLevel, chunked);
            byte[] attributesCompressed = CompressionHelpers.Compress(attributeBuffer, Compression, CompressionLevel, chunked);
            byte[] valuesCompressed = CompressionHelpers.Compress(valueBuffer, Compression, CompressionLevel, chunked);
            byte[] keysCompressed = MetadataFormat == LSFMetadataFormat.KeysAndAdjacency
                ? CompressionHelpers.Compress(keyBuffer, Compression, CompressionLevel, chunked)
                : [];

            if (Version < LSFVersion.VerBG3NodeKeys)
            {
                var meta = new LSFMetadataV5
                {
                    StringsUncompressedSize = (uint)stringBuffer.Length,
                    NodesUncompressedSize = (uint)nodeBuffer.Length,
                    AttributesUncompressedSize = (uint)attributeBuffer.Length,
                    ValuesUncompressedSize = (uint)valueBuffer.Length,
                    StringsSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)stringsCompressed.Length,
                    NodesSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)nodesCompressed.Length,
                    AttributesSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)attributesCompressed.Length,
                    ValuesSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)valuesCompressed.Length,
                    CompressionFlags = CompressionHelpers.MakeCompressionFlags(Compression, CompressionLevel),
                    MetadataFormat = MetadataFormat
                };
                BinUtils.WriteStruct(Writer, ref meta);
            }
            else
            {
                var meta = new LSFMetadataV6
                {
                    StringsUncompressedSize = (uint)stringBuffer.Length,
                    KeysUncompressedSize = (uint)keyBuffer.Length,
                    NodesUncompressedSize = (uint)nodeBuffer.Length,
                    AttributesUncompressedSize = (uint)attributeBuffer.Length,
                    ValuesUncompressedSize = (uint)valueBuffer.Length,
                    StringsSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)stringsCompressed.Length,
                    KeysSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)keysCompressed.Length,
                    NodesSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)nodesCompressed.Length,
                    AttributesSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)attributesCompressed.Length,
                    ValuesSizeOnDisk = Compression == LSLibCompressionMethod.None ? 0 : (uint)valuesCompressed.Length,
                    CompressionFlags = CompressionHelpers.MakeCompressionFlags(Compression, CompressionLevel),
                    MetadataFormat = MetadataFormat
                };
                BinUtils.WriteStruct(Writer, ref meta);
            }

            Writer.Write(stringsCompressed);
            Writer.Write(nodesCompressed);
            Writer.Write(attributesCompressed);
            Writer.Write(valuesCompressed);
            Writer.Write(keysCompressed);
        }
    }

    private int ComputeSiblingIndices(Node node)
    {
        int index = NextNodeIndex++;
        NextSiblingIndices!.Add(-1);
        int lastSiblingIndex = -1;
        foreach (var children in node.Children)
            foreach (var child in children.Value)
            {
                int childIndex = ComputeSiblingIndices(child);
                if (lastSiblingIndex != -1)
                    NextSiblingIndices[lastSiblingIndex] = childIndex;
                lastSiblingIndex = childIndex;
            }
        return index;
    }

    private void ComputeSiblingIndices(Resource resource)
    {
        NextNodeIndex = 0;
        NextSiblingIndices = [];
        int lastRegionIndex = -1;
        foreach (var region in resource.Regions)
        {
            int regionIndex = ComputeSiblingIndices(region.Value);
            if (lastRegionIndex != -1)
                NextSiblingIndices[lastRegionIndex] = regionIndex;
            lastRegionIndex = regionIndex;
        }
    }

    private void WriteRegions(Resource resource)
    {
        NextNodeIndex = 0;
        foreach (var region in resource.Regions)
        {
            if (Version >= LSFVersion.VerExtendedNodes && MetadataFormat == LSFMetadataFormat.KeysAndAdjacency)
                WriteNodeV3(region.Value);
            else
                WriteNodeV2(region.Value);
        }
    }

    private void WriteNodeAttributesV2(Node node)
    {
        uint lastOffset = (uint)ValueStream.Position;
        foreach (var entry in node.Attributes)
        {
            WriteAttributeValue(ValueWriter, entry.Value);
            var attributeInfo = new LSFAttributeEntryV2
            {
                TypeAndLength = (uint)entry.Value.Type | (((uint)ValueStream.Position - lastOffset) << 6),
                NameHashTableIndex = AddStaticString(entry.Key),
                NodeIndex = NextNodeIndex
            };
            BinUtils.WriteStruct(AttributeWriter, ref attributeInfo);
            NextAttributeIndex++;
            lastOffset = (uint)ValueStream.Position;
        }
    }

    private void WriteNodeAttributesV3(Node node)
    {
        uint lastOffset = (uint)ValueStream.Position;
        int numWritten = 0;
        foreach (var entry in node.Attributes)
        {
            WriteAttributeValue(ValueWriter, entry.Value);
            numWritten++;
            var attributeInfo = new LSFAttributeEntryV3
            {
                TypeAndLength = (uint)entry.Value.Type | (((uint)ValueStream.Position - lastOffset) << 6),
                NameHashTableIndex = AddStaticString(entry.Key),
                NextAttributeIndex = numWritten == node.Attributes.Count ? -1 : NextAttributeIndex + 1,
                Offset = lastOffset
            };
            BinUtils.WriteStruct(AttributeWriter, ref attributeInfo);
            NextAttributeIndex++;
            lastOffset = (uint)ValueStream.Position;
        }
    }

    private void WriteNodeChildren(Node node)
    {
        foreach (var children in node.Children)
            foreach (var child in children.Value)
            {
                if (Version >= LSFVersion.VerExtendedNodes && MetadataFormat == LSFMetadataFormat.KeysAndAdjacency)
                    WriteNodeV3(child);
                else
                    WriteNodeV2(child);
            }
    }

    private void WriteNodeV2(Node node)
    {
        var nodeInfo = new LSFNodeEntryV2
        {
            ParentIndex = node.Parent == null ? -1 : NodeIndices[node.Parent],
            NameHashTableIndex = AddStaticString(node.Name),
            FirstAttributeIndex = node.Attributes.Count > 0 ? NextAttributeIndex : -1
        };
        if (node.Attributes.Count > 0)
            WriteNodeAttributesV2(node);
        BinUtils.WriteStruct(NodeWriter, ref nodeInfo);
        NodeIndices[node] = NextNodeIndex++;
        WriteNodeChildren(node);
    }

    private void WriteNodeV3(Node node)
    {
        var nodeInfo = new LSFNodeEntryV3
        {
            ParentIndex = node.Parent == null ? -1 : NodeIndices[node.Parent],
            NameHashTableIndex = AddStaticString(node.Name),
            NextSiblingIndex = NextSiblingIndices![NextNodeIndex],
            FirstAttributeIndex = node.Attributes.Count > 0 ? NextAttributeIndex : -1
        };
        if (node.Attributes.Count > 0)
            WriteNodeAttributesV3(node);
        BinUtils.WriteStruct(NodeWriter, ref nodeInfo);

        if (node.KeyAttribute != null && MetadataFormat == LSFMetadataFormat.KeysAndAdjacency)
        {
            var keyInfo = new LSFKeyEntry
            {
                NodeIndex = (uint)NextNodeIndex,
                KeyName = AddStaticString(node.KeyAttribute)
            };
            BinUtils.WriteStruct(KeyWriter, ref keyInfo);
        }

        NodeIndices[node] = NextNodeIndex++;
        WriteNodeChildren(node);
    }

    private void WriteTranslatedFSString(BinaryWriter writer, TranslatedFSString fs)
    {
        if (Version >= LSFVersion.VerBG3 ||
            (Meta.MajorVersion > 4 ||
            (Meta.MajorVersion == 4 && Meta.Revision > 0) ||
            (Meta.MajorVersion == 4 && Meta.Revision == 0 && Meta.BuildNumber >= 0x1a)))
        {
            writer.Write(fs.Version);
        }
        else
        {
            WriteStringWithLength(writer, fs.Value ?? "");
        }
        WriteStringWithLength(writer, fs.Handle);
        writer.Write((uint)fs.Arguments.Count);
        foreach (var arg in fs.Arguments)
        {
            WriteStringWithLength(writer, arg.Key);
            WriteTranslatedFSString(writer, arg.String);
            WriteStringWithLength(writer, arg.Value);
        }
    }

    private void WriteAttributeValue(BinaryWriter writer, NodeAttribute attr)
    {
        switch (attr.Type)
        {
            case AttributeType.String:
            case AttributeType.Path:
            case AttributeType.FixedString:
            case AttributeType.LSString:
            case AttributeType.WString:
            case AttributeType.LSWString:
                WriteString(writer, (string)attr.Value);
                break;
            case AttributeType.TranslatedString:
                var ts = (TranslatedString)attr.Value;
                if (Version >= LSFVersion.VerBG3)
                    writer.Write(ts.Version);
                else
                    WriteStringWithLength(writer, ts.Value ?? "");
                WriteStringWithLength(writer, ts.Handle);
                break;
            case AttributeType.TranslatedFSString:
                WriteTranslatedFSString(writer, (TranslatedFSString)attr.Value);
                break;
            case AttributeType.ScratchBuffer:
                writer.Write((byte[])attr.Value);
                break;
            default:
                BinUtils.WriteAttribute(writer, attr);
                break;
        }
    }

    private uint AddStaticString(string s)
    {
        var hashCode = (uint)s.GetHashCode();
        var bucket = (int)((hashCode & 0x1ff) ^ ((hashCode >> 9) & 0x1ff) ^ ((hashCode >> 18) & 0x1ff) ^ ((hashCode >> 27) & 0x1ff));
        for (int i = 0; i < StringHashMap[bucket].Count; i++)
            if (StringHashMap[bucket][i].Equals(s))
                return (uint)((bucket << 16) | i);
        StringHashMap[bucket].Add(s);
        return (uint)((bucket << 16) | (StringHashMap[bucket].Count - 1));
    }

    private void WriteStaticStrings(BinaryWriter writer)
    {
        writer.Write((uint)StringHashMap.Count);
        foreach (var entry in StringHashMap)
        {
            writer.Write((ushort)entry.Count);
            foreach (var s in entry)
            {
                byte[] utf = Encoding.UTF8.GetBytes(s);
                writer.Write((ushort)utf.Length);
                writer.Write(utf);
            }
        }
    }

    private static void WriteStringWithLength(BinaryWriter writer, string s)
    {
        byte[] utf = Encoding.UTF8.GetBytes(s);
        writer.Write((int)(utf.Length + 1));
        writer.Write(utf);
        writer.Write((byte)0);
    }

    private static void WriteString(BinaryWriter writer, string s)
    {
        byte[] utf = Encoding.UTF8.GetBytes(s);
        writer.Write(utf);
        writer.Write((byte)0);
    }
}
