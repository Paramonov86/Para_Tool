using System.Security.Cryptography;
using K4os.Compression.LZ4;
using ParaTool.Core.Models;

namespace ParaTool.Core;

public static class PakWriter
{
    private const byte PadByte = 0xAD;

    public static void CreatePak(string inputDir, string outputPak, byte flags = 0, byte priority = 0)
    {
        var files = Directory.GetFiles(inputDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(inputDir, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        using var fs = File.Create(outputPak);
        using var bw = new BinaryWriter(fs);

        // Write placeholder header (40 bytes)
        bw.Write(new byte[LspkHeader.Size]);

        var entries = new List<FileEntry>(files.Count);

        // Write file data
        foreach (var relativePath in files)
        {
            var fullPath = Path.Combine(inputDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var rawData = File.ReadAllBytes(fullPath);

            // LZ4 compress
            var maxCompressed = LZ4Codec.MaximumOutputSize(rawData.Length);
            var compressedBuf = new byte[maxCompressed];
            int compressedSize = LZ4Codec.Encode(rawData, 0, rawData.Length,
                                                   compressedBuf, 0, compressedBuf.Length);

            // Use compressed only if smaller
            byte[] diskData;
            byte entryFlags;
            if (compressedSize > 0 && compressedSize < rawData.Length)
            {
                diskData = new byte[compressedSize];
                Buffer.BlockCopy(compressedBuf, 0, diskData, 0, compressedSize);
                entryFlags = (byte)CompressionMethod.LZ4;
            }
            else
            {
                diskData = rawData;
                entryFlags = (byte)CompressionMethod.None;
            }

            long offset = fs.Position;
            bw.Write(diskData);

            // Pad to 8-byte alignment
            long pos = fs.Position;
            long aligned = (pos + 7) & ~7L;
            for (long p = pos; p < aligned; p++)
                bw.Write(PadByte);

            entries.Add(new FileEntry
            {
                Path = relativePath,
                OffsetLo = (uint)(offset & 0xFFFFFFFF),
                OffsetHi = (ushort)(offset >> 32),
                ArchivePart = 0,
                Flags = entryFlags,
                DiskSize = (uint)diskData.Length,
                UncompressedSize = (uint)rawData.Length
            });
        }

        // Write file list
        long fileListOffset = fs.Position;

        // Serialize entries to raw bytes
        int rawSize = entries.Count * FileEntry.Size;
        var rawEntries = new byte[rawSize];
        using (var ms = new MemoryStream(rawEntries))
        using (var entryWriter = new BinaryWriter(ms))
        {
            foreach (var entry in entries)
                entry.Write(entryWriter);
        }

        // LZ4 compress file list
        var maxCl = LZ4Codec.MaximumOutputSize(rawSize);
        var clBuf = new byte[maxCl];
        int clSize = LZ4Codec.Encode(rawEntries, 0, rawSize, clBuf, 0, clBuf.Length);

        bw.Write((uint)entries.Count);
        bw.Write((uint)clSize);
        bw.Write(clBuf, 0, clSize);

        uint fileListSize = (uint)(4 + 4 + clSize);

        // Compute MD5 over file data region (from after header to before file list)
        byte[] md5Hash;
        fs.Position = LspkHeader.Size;
        long dataLength = fileListOffset - LspkHeader.Size;
        using (var md5 = MD5.Create())
        {
            var buffer = new byte[81920];
            long remaining = dataLength;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = fs.Read(buffer, 0, toRead);
                if (read == 0) break;
                md5.TransformBlock(buffer, 0, read, null, 0);
                remaining -= read;
            }
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            md5Hash = md5.Hash!;
        }

        // Write final header
        fs.Position = 0;
        var header = new LspkHeader
        {
            Version = LspkHeader.ExpectedVersion,
            FileListOffset = (ulong)fileListOffset,
            FileListSize = fileListSize,
            Flags = flags,
            Priority = priority,
            Md5 = md5Hash,
            NumParts = 1
        };
        header.Write(bw);
    }
}
