using System.IO.Compression;
using K4os.Compression.LZ4;
using ParaTool.Core.Models;

namespace ParaTool.Core;

public static class PakReader
{
    public static LspkHeader ReadHeader(Stream stream)
    {
        stream.Position = 0;
        using var br = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return LspkHeader.Read(br);
    }

    public static List<FileEntry> ReadFileList(Stream stream, LspkHeader header)
    {
        stream.Position = (long)header.FileListOffset;
        using var br = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        uint numFiles = br.ReadUInt32();
        uint compressedSize = br.ReadUInt32();

        var compressedData = br.ReadBytes((int)compressedSize);
        int uncompressedSize = (int)(numFiles * FileEntry.Size);
        var uncompressedData = new byte[uncompressedSize];

        int decoded = LZ4Codec.Decode(compressedData, 0, compressedData.Length,
                                       uncompressedData, 0, uncompressedData.Length);
        if (decoded != uncompressedSize)
            throw new InvalidDataException(
                $"FileList LZ4 decode mismatch: expected {uncompressedSize}, got {decoded}.");

        var entries = new List<FileEntry>((int)numFiles);
        using var ms = new MemoryStream(uncompressedData);
        using var entryReader = new BinaryReader(ms);
        for (int i = 0; i < numFiles; i++)
        {
            entries.Add(FileEntry.Read(entryReader));
        }

        return entries;
    }

    public static byte[] ExtractFileData(Stream stream, FileEntry entry)
    {
        stream.Position = entry.FullOffset;
        var diskData = new byte[entry.DiskSize];
        stream.ReadExactly(diskData);

        return entry.Compression switch
        {
            CompressionMethod.None => diskData,
            CompressionMethod.LZ4 => DecompressLZ4(diskData, (int)entry.UncompressedSize),
            CompressionMethod.Zlib => DecompressZlib(diskData, (int)entry.UncompressedSize),
            _ => throw new NotSupportedException($"Unknown compression {entry.Compression}")
        };
    }

    public static void ExtractAll(string pakPath, string outputDir, IProgress<int>? progress = null)
    {
        using var fs = File.OpenRead(pakPath);
        var header = ReadHeader(fs);
        var entries = ReadFileList(fs, header);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var outPath = Path.Combine(outputDir, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            var data = ExtractFileData(fs, entry);
            File.WriteAllBytes(outPath, data);

            progress?.Report(i + 1);
        }
    }

    public static void ExtractFile(Stream stream, FileEntry entry, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var data = ExtractFileData(stream, entry);
        File.WriteAllBytes(outputPath, data);
    }

    private static byte[] DecompressLZ4(byte[] compressed, int uncompressedSize)
    {
        var output = new byte[uncompressedSize];
        int decoded = LZ4Codec.Decode(compressed, 0, compressed.Length, output, 0, output.Length);
        if (decoded != uncompressedSize)
            throw new InvalidDataException(
                $"LZ4 decode: expected {uncompressedSize}, got {decoded}.");
        return output;
    }

    private static byte[] DecompressZlib(byte[] compressed, int uncompressedSize)
    {
        using var ms = new MemoryStream(compressed);
        using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
        var output = new byte[uncompressedSize];
        int totalRead = 0;
        while (totalRead < uncompressedSize)
        {
            int read = zlib.Read(output, totalRead, uncompressedSize - totalRead);
            if (read == 0) break;
            totalRead += read;
        }
        return output;
    }
}
