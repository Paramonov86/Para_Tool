namespace ParaTool.Core.Models;

/// <summary>
/// LSPK v18 header — 40 bytes, little-endian.
/// </summary>
public struct LspkHeader
{
    public const int Size = 40;
    public static readonly byte[] Magic = "LSPK"u8.ToArray();
    public const uint ExpectedVersion = 18;

    public uint Version;
    public ulong FileListOffset;
    public uint FileListSize;
    public byte Flags;
    public byte Priority;
    public byte[] Md5;   // 16 bytes
    public ushort NumParts;

    public static LspkHeader Read(BinaryReader br)
    {
        var magic = br.ReadBytes(4);
        if (magic.Length < 4 || magic[0] != 'L' || magic[1] != 'S' || magic[2] != 'P' || magic[3] != 'K')
            throw new InvalidDataException("Not an LSPK file — bad magic bytes.");

        var header = new LspkHeader
        {
            Version = br.ReadUInt32()
        };

        if (header.Version != ExpectedVersion)
            throw new InvalidDataException($"Unsupported LSPK version {header.Version}, expected {ExpectedVersion}.");

        header.FileListOffset = br.ReadUInt64();
        header.FileListSize = br.ReadUInt32();
        header.Flags = br.ReadByte();
        header.Priority = br.ReadByte();
        header.Md5 = br.ReadBytes(16);
        header.NumParts = br.ReadUInt16();

        return header;
    }

    public void Write(BinaryWriter bw)
    {
        bw.Write(Magic);
        bw.Write(ExpectedVersion);
        bw.Write(FileListOffset);
        bw.Write(FileListSize);
        bw.Write(Flags);
        bw.Write(Priority);
        bw.Write(Md5 ?? new byte[16]);
        bw.Write(NumParts);
    }
}
