using System.Text;

namespace ParaTool.Core.Models;

public enum CompressionMethod : byte
{
    None = 0,
    Zlib = 1,
    LZ4 = 2
}

/// <summary>
/// LSPK v18 file entry — 272 bytes.
/// </summary>
public struct FileEntry
{
    public const int Size = 272;
    public const int PathFieldSize = 256;

    public string Path;
    public uint OffsetLo;
    public ushort OffsetHi;
    public byte ArchivePart;
    public byte Flags;
    public uint DiskSize;
    public uint UncompressedSize;

    public long FullOffset => OffsetLo | ((long)OffsetHi << 32);

    public CompressionMethod Compression => (CompressionMethod)(Flags & 0x0F);

    public static FileEntry Read(BinaryReader br)
    {
        var pathBytes = br.ReadBytes(PathFieldSize);
        int nullIdx = Array.IndexOf(pathBytes, (byte)0);
        var path = Encoding.UTF8.GetString(pathBytes, 0, nullIdx >= 0 ? nullIdx : PathFieldSize);

        return new FileEntry
        {
            Path = path,
            OffsetLo = br.ReadUInt32(),
            OffsetHi = br.ReadUInt16(),
            ArchivePart = br.ReadByte(),
            Flags = br.ReadByte(),
            DiskSize = br.ReadUInt32(),
            UncompressedSize = br.ReadUInt32()
        };
    }

    public void Write(BinaryWriter bw)
    {
        var pathBytes = new byte[PathFieldSize];
        Encoding.UTF8.GetBytes(Path, 0, Path.Length, pathBytes, 0);
        bw.Write(pathBytes);
        bw.Write(OffsetLo);
        bw.Write(OffsetHi);
        bw.Write(ArchivePart);
        bw.Write(Flags);
        bw.Write(DiskSize);
        bw.Write(UncompressedSize);
    }
}
