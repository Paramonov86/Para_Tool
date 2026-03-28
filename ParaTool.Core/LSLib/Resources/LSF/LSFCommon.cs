using System.Runtime.InteropServices;

namespace ParaTool.Core.LSLib;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFMagic
{
    public readonly static byte[] Signature = "LSOF"u8.ToArray();
    public UInt32 Magic;
    public UInt32 Version;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFHeader
{
    public Int32 EngineVersion;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFHeaderV5
{
    public Int64 EngineVersion;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFMetadataV5
{
    public UInt32 StringsUncompressedSize;
    public UInt32 StringsSizeOnDisk;
    public UInt32 NodesUncompressedSize;
    public UInt32 NodesSizeOnDisk;
    public UInt32 AttributesUncompressedSize;
    public UInt32 AttributesSizeOnDisk;
    public UInt32 ValuesUncompressedSize;
    public UInt32 ValuesSizeOnDisk;
    public CompressionFlags CompressionFlags;
    public Byte Unknown2;
    public UInt16 Unknown3;
    public LSFMetadataFormat MetadataFormat;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFMetadataV6
{
    public UInt32 StringsUncompressedSize;
    public UInt32 StringsSizeOnDisk;
    public UInt32 KeysUncompressedSize;
    public UInt32 KeysSizeOnDisk;
    public UInt32 NodesUncompressedSize;
    public UInt32 NodesSizeOnDisk;
    public UInt32 AttributesUncompressedSize;
    public UInt32 AttributesSizeOnDisk;
    public UInt32 ValuesUncompressedSize;
    public UInt32 ValuesSizeOnDisk;
    public CompressionFlags CompressionFlags;
    public Byte Unknown2;
    public UInt16 Unknown3;
    public LSFMetadataFormat MetadataFormat;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFNodeEntryV2
{
    public UInt32 NameHashTableIndex;
    public Int32 FirstAttributeIndex;
    public Int32 ParentIndex;
    public int NameIndex => (int)(NameHashTableIndex >> 16);
    public int NameOffset => (int)(NameHashTableIndex & 0xffff);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFNodeEntryV3
{
    public UInt32 NameHashTableIndex;
    public Int32 ParentIndex;
    public Int32 NextSiblingIndex;
    public Int32 FirstAttributeIndex;
    public int NameIndex => (int)(NameHashTableIndex >> 16);
    public int NameOffset => (int)(NameHashTableIndex & 0xffff);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFKeyEntry
{
    public UInt32 NodeIndex;
    public UInt32 KeyName;
    public int KeyNameIndex => (int)(KeyName >> 16);
    public int KeyNameOffset => (int)(KeyName & 0xffff);
}

internal class LSFNodeInfo
{
    public int ParentIndex;
    public int NameIndex;
    public int NameOffset;
    public int FirstAttributeIndex;
    public string? KeyAttribute = null;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFAttributeEntryV2
{
    public UInt32 NameHashTableIndex;
    public UInt32 TypeAndLength;
    public Int32 NodeIndex;
    public int NameIndex => (int)(NameHashTableIndex >> 16);
    public int NameOffset => (int)(NameHashTableIndex & 0xffff);
    public uint TypeId => TypeAndLength & 0x3f;
    public uint Length => TypeAndLength >> 6;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LSFAttributeEntryV3
{
    public UInt32 NameHashTableIndex;
    public UInt32 TypeAndLength;
    public Int32 NextAttributeIndex;
    public UInt32 Offset;
    public int NameIndex => (int)(NameHashTableIndex >> 16);
    public int NameOffset => (int)(NameHashTableIndex & 0xffff);
    public uint TypeId => TypeAndLength & 0x3f;
    public uint Length => TypeAndLength >> 6;
}

internal class LSFAttributeInfo
{
    public int NameIndex;
    public int NameOffset;
    public uint TypeId;
    public uint Length;
    public uint DataOffset;
    public int NextAttributeIndex;
}
