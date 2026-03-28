using System.Text;

namespace ParaTool.Core.LSLib;

public class LSFReader(Stream stream, bool keepOpen = false) : IDisposable
{
    private readonly Stream Stream = stream;
    private List<List<string>> Names = null!;
    private List<LSFNodeInfo> Nodes = null!;
    private List<LSFAttributeInfo> Attributes = null!;
    private List<Node> NodeInstances = null!;
    private Stream Values = null!;
    private LSFVersion Version;
    private PackedVersion GameVersion;
    private LSFMetadataV6 Metadata;

    public void Dispose()
    {
        if (!keepOpen) Stream.Dispose();
    }

    private void ReadNames(Stream s)
    {
        using var reader = new BinaryReader(s);
        var numHashEntries = reader.ReadUInt32();
        while (numHashEntries-- > 0)
        {
            var hash = new List<string>();
            Names.Add(hash);
            var numStrings = reader.ReadUInt16();
            while (numStrings-- > 0)
            {
                var nameLen = reader.ReadUInt16();
                byte[] bytes = reader.ReadBytes(nameLen);
                hash.Add(Encoding.UTF8.GetString(bytes));
            }
        }
    }

    private void ReadNodes(Stream s, bool longNodes)
    {
        using var reader = new BinaryReader(s);
        while (s.Position < s.Length)
        {
            var resolved = new LSFNodeInfo();
            if (longNodes)
            {
                var item = BinUtils.ReadStruct<LSFNodeEntryV3>(reader);
                resolved.ParentIndex = item.ParentIndex;
                resolved.NameIndex = item.NameIndex;
                resolved.NameOffset = item.NameOffset;
                resolved.FirstAttributeIndex = item.FirstAttributeIndex;
            }
            else
            {
                var item = BinUtils.ReadStruct<LSFNodeEntryV2>(reader);
                resolved.ParentIndex = item.ParentIndex;
                resolved.NameIndex = item.NameIndex;
                resolved.NameOffset = item.NameOffset;
                resolved.FirstAttributeIndex = item.FirstAttributeIndex;
            }
            Nodes.Add(resolved);
        }
    }

    private void ReadAttributesV2(Stream s)
    {
        using var reader = new BinaryReader(s);
        var prevAttributeRefs = new List<int>();
        uint dataOffset = 0;
        int index = 0;
        while (s.Position < s.Length)
        {
            var attribute = BinUtils.ReadStruct<LSFAttributeEntryV2>(reader);
            var resolved = new LSFAttributeInfo
            {
                NameIndex = attribute.NameIndex,
                NameOffset = attribute.NameOffset,
                TypeId = attribute.TypeId,
                Length = attribute.Length,
                DataOffset = dataOffset,
                NextAttributeIndex = -1
            };

            var nodeIndex = attribute.NodeIndex + 1;
            if (prevAttributeRefs.Count > nodeIndex)
            {
                if (prevAttributeRefs[nodeIndex] != -1)
                    Attributes[prevAttributeRefs[nodeIndex]].NextAttributeIndex = index;
                prevAttributeRefs[nodeIndex] = index;
            }
            else
            {
                while (prevAttributeRefs.Count < nodeIndex)
                    prevAttributeRefs.Add(-1);
                prevAttributeRefs.Add(index);
            }

            dataOffset += resolved.Length;
            Attributes.Add(resolved);
            index++;
        }
    }

    private void ReadAttributesV3(Stream s)
    {
        using var reader = new BinaryReader(s);
        while (s.Position < s.Length)
        {
            var attribute = BinUtils.ReadStruct<LSFAttributeEntryV3>(reader);
            Attributes.Add(new LSFAttributeInfo
            {
                NameIndex = attribute.NameIndex,
                NameOffset = attribute.NameOffset,
                TypeId = attribute.TypeId,
                Length = attribute.Length,
                DataOffset = attribute.Offset,
                NextAttributeIndex = attribute.NextAttributeIndex
            });
        }
    }

    private void ReadKeys(Stream s)
    {
        using var reader = new BinaryReader(s);
        while (s.Position < s.Length)
        {
            var key = BinUtils.ReadStruct<LSFKeyEntry>(reader);
            var keyAttribute = Names[key.KeyNameIndex][key.KeyNameOffset];
            Nodes[(int)key.NodeIndex].KeyAttribute = keyAttribute;
        }
    }

    private MemoryStream Decompress(BinaryReader reader, uint sizeOnDisk, uint uncompressedSize, bool allowChunked)
    {
        if (sizeOnDisk == 0 && uncompressedSize != 0)
            return new MemoryStream(reader.ReadBytes((int)uncompressedSize));

        if (sizeOnDisk == 0 && uncompressedSize == 0)
            return new MemoryStream();

        bool chunked = Version >= LSFVersion.VerChunkedCompress && allowChunked;
        bool isCompressed = Metadata.CompressionFlags.Method() != LSLibCompressionMethod.None;
        uint compressedSize = isCompressed ? sizeOnDisk : uncompressedSize;
        byte[] compressed = reader.ReadBytes((int)compressedSize);
        var uncompressed = CompressionHelpers.Decompress(compressed, (int)uncompressedSize, Metadata.CompressionFlags, chunked);
        return new MemoryStream(uncompressed);
    }

    private void ReadHeaders(BinaryReader reader)
    {
        var magic = BinUtils.ReadStruct<LSFMagic>(reader);
        if (magic.Magic != BitConverter.ToUInt32(LSFMagic.Signature, 0))
            throw new InvalidDataException($"Invalid LSF signature: 0x{magic.Magic:X8}");

        if (magic.Version < (uint)LSFVersion.VerInitial || magic.Version > (uint)LSFVersion.MaxReadVersion)
            throw new InvalidDataException($"LSF version {magic.Version} is not supported");

        Version = (LSFVersion)magic.Version;

        if (Version >= LSFVersion.VerBG3ExtendedHeader)
        {
            var hdr = BinUtils.ReadStruct<LSFHeaderV5>(reader);
            GameVersion = PackedVersion.FromInt64(hdr.EngineVersion);
            if (GameVersion.Major == 0)
            {
                GameVersion.Major = 4;
                GameVersion.Minor = 0;
                GameVersion.Revision = 9;
                GameVersion.Build = 0;
            }
        }
        else
        {
            var hdr = BinUtils.ReadStruct<LSFHeader>(reader);
            GameVersion = PackedVersion.FromInt32(hdr.EngineVersion);
        }

        if (Version < LSFVersion.VerBG3NodeKeys)
        {
            var meta = BinUtils.ReadStruct<LSFMetadataV5>(reader);
            Metadata = new LSFMetadataV6
            {
                StringsUncompressedSize = meta.StringsUncompressedSize,
                StringsSizeOnDisk = meta.StringsSizeOnDisk,
                NodesUncompressedSize = meta.NodesUncompressedSize,
                NodesSizeOnDisk = meta.NodesSizeOnDisk,
                AttributesUncompressedSize = meta.AttributesUncompressedSize,
                AttributesSizeOnDisk = meta.AttributesSizeOnDisk,
                ValuesUncompressedSize = meta.ValuesUncompressedSize,
                ValuesSizeOnDisk = meta.ValuesSizeOnDisk,
                CompressionFlags = meta.CompressionFlags,
                MetadataFormat = meta.MetadataFormat
            };
        }
        else
        {
            Metadata = BinUtils.ReadStruct<LSFMetadataV6>(reader);
        }
    }

    public Resource Read()
    {
        using var reader = new BinaryReader(Stream);
        ReadHeaders(reader);

        Names = [];
        using (var namesStream = Decompress(reader, Metadata.StringsSizeOnDisk, Metadata.StringsUncompressedSize, false))
            ReadNames(namesStream);

        Nodes = [];
        var hasAdj = Version >= LSFVersion.VerExtendedNodes && Metadata.MetadataFormat == LSFMetadataFormat.KeysAndAdjacency;
        using (var nodesStream = Decompress(reader, Metadata.NodesSizeOnDisk, Metadata.NodesUncompressedSize, true))
            ReadNodes(nodesStream, hasAdj);

        Attributes = [];
        using (var attrStream = Decompress(reader, Metadata.AttributesSizeOnDisk, Metadata.AttributesUncompressedSize, true))
        {
            if (hasAdj) ReadAttributesV3(attrStream);
            else ReadAttributesV2(attrStream);
        }

        Values = Decompress(reader, Metadata.ValuesSizeOnDisk, Metadata.ValuesUncompressedSize, true);

        if (Metadata.MetadataFormat == LSFMetadataFormat.KeysAndAdjacency)
        {
            using var keysStream = Decompress(reader, Metadata.KeysSizeOnDisk, Metadata.KeysUncompressedSize, true);
            ReadKeys(keysStream);
        }

        var resource = new Resource { MetadataFormat = Metadata.MetadataFormat };
        ReadRegions(resource);
        resource.Metadata.MajorVersion = GameVersion.Major;
        resource.Metadata.MinorVersion = GameVersion.Minor;
        resource.Metadata.Revision = GameVersion.Revision;
        resource.Metadata.BuildNumber = GameVersion.Build;
        return resource;
    }

    private void ReadRegions(Resource resource)
    {
        var attrReader = new BinaryReader(Values);
        NodeInstances = [];
        for (int i = 0; i < Nodes.Count; i++)
        {
            var defn = Nodes[i];
            if (defn.ParentIndex == -1)
            {
                var region = new Region();
                ReadNode(defn, region, attrReader);
                region.KeyAttribute = defn.KeyAttribute;
                NodeInstances.Add(region);
                region.RegionName = region.Name;
                resource.Regions[region.Name] = region;
            }
            else
            {
                var node = new Node();
                ReadNode(defn, node, attrReader);
                node.KeyAttribute = defn.KeyAttribute;
                node.Parent = NodeInstances[defn.ParentIndex];
                NodeInstances.Add(node);
                NodeInstances[defn.ParentIndex].AppendChild(node);
            }
        }
    }

    private void ReadNode(LSFNodeInfo defn, Node node, BinaryReader attributeReader)
    {
        node.Name = Names[defn.NameIndex][defn.NameOffset];

        if (defn.FirstAttributeIndex != -1)
        {
            var attribute = Attributes[defn.FirstAttributeIndex];
            while (true)
            {
                Values.Position = attribute.DataOffset;
                var value = ReadAttribute((AttributeType)attribute.TypeId, attributeReader, attribute.Length);
                node.Attributes[Names[attribute.NameIndex][attribute.NameOffset]] = value;

                if (attribute.NextAttributeIndex == -1) break;
                attribute = Attributes[attribute.NextAttributeIndex];
            }
        }
    }

    private NodeAttribute ReadAttribute(AttributeType type, BinaryReader reader, uint length)
    {
        switch (type)
        {
            case AttributeType.String:
            case AttributeType.Path:
            case AttributeType.FixedString:
            case AttributeType.LSString:
            case AttributeType.WString:
            case AttributeType.LSWString:
                return new NodeAttribute(type) { Value = ReadString(reader, (int)length) };

            case AttributeType.TranslatedString:
                {
                    var str = new TranslatedString();
                    if (Version >= LSFVersion.VerBG3 ||
                        (GameVersion.Major > 4 ||
                        (GameVersion.Major == 4 && GameVersion.Revision > 0) ||
                        (GameVersion.Major == 4 && GameVersion.Revision == 0 && GameVersion.Build >= 0x1a)))
                    {
                        str.Version = reader.ReadUInt16();
                    }
                    else
                    {
                        str.Version = 0;
                        var valueLength = reader.ReadInt32();
                        str.Value = ReadString(reader, valueLength);
                    }
                    var handleLength = reader.ReadInt32();
                    str.Handle = ReadString(reader, handleLength);
                    return new NodeAttribute(type) { Value = str };
                }

            case AttributeType.TranslatedFSString:
                return new NodeAttribute(type) { Value = ReadTranslatedFSString(reader) };

            case AttributeType.ScratchBuffer:
                return new NodeAttribute(type) { Value = reader.ReadBytes((int)length) };

            default:
                return BinUtils.ReadAttribute(type, reader);
        }
    }

    private TranslatedFSString ReadTranslatedFSString(BinaryReader reader)
    {
        var str = new TranslatedFSString();
        if (Version >= LSFVersion.VerBG3)
        {
            str.Version = reader.ReadUInt16();
        }
        else
        {
            str.Version = 0;
            var valueLength = reader.ReadInt32();
            str.Value = ReadString(reader, valueLength);
        }
        var handleLength = reader.ReadInt32();
        str.Handle = ReadString(reader, handleLength);

        var arguments = reader.ReadInt32();
        str.Arguments = new List<TranslatedFSStringArgument>(arguments);
        for (int i = 0; i < arguments; i++)
        {
            var arg = new TranslatedFSStringArgument();
            var argKeyLength = reader.ReadInt32();
            arg.Key = ReadString(reader, argKeyLength);
            arg.String = ReadTranslatedFSString(reader);
            var argValueLength = reader.ReadInt32();
            arg.Value = ReadString(reader, argValueLength);
            str.Arguments.Add(arg);
        }
        return str;
    }

    private static string ReadString(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length - 1);
        int lastNull = bytes.Length;
        while (lastNull > 0 && bytes[lastNull - 1] == 0) lastNull--;
        reader.ReadByte(); // null terminator
        return Encoding.UTF8.GetString(bytes, 0, lastNull);
    }
}
