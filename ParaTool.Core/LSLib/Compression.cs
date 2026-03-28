using System.Buffers;
using System.IO.Compression;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace ParaTool.Core.LSLib;

public static class CompressionHelpers
{
    public static CompressionFlags MakeCompressionFlags(LSLibCompressionMethod method, LSCompressionLevel level)
    {
        if (method == LSLibCompressionMethod.None) return 0;
        return method.ToFlags() | level.ToFlags();
    }

    public static byte[] Decompress(byte[] compressed, int decompressedSize, CompressionFlags compression, bool chunked = false)
    {
        switch (compression.Method())
        {
            case LSLibCompressionMethod.None:
                return compressed;

            case LSLibCompressionMethod.Zlib:
                {
                    using var compressedStream = new MemoryStream(compressed);
                    using var decompressedStream = new MemoryStream();
                    using var stream = new ZLibStream(compressedStream, CompressionMode.Decompress);
                    stream.CopyTo(decompressedStream);
                    return decompressedStream.ToArray();
                }

            case LSLibCompressionMethod.LZ4:
                if (chunked)
                {
                    using var input = new MemoryStream(compressed);
                    using var output = new MemoryStream();
                    using var decompressor = LZ4Stream.Decode(input);
                    var temp = ArrayPool<byte>.Shared.Rent(0x10000);
                    try
                    {
                        while (decompressedSize > 0)
                        {
                            int count = decompressor.Read(temp, 0, Math.Min(decompressedSize, temp.Length));
                            if (count == 0) break;
                            output.Write(temp, 0, count);
                            decompressedSize -= count;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(temp);
                    }
                    return output.ToArray();
                }
                else
                {
                    var decompressed = new byte[decompressedSize];
                    int resultSize = LZ4Codec.Decode(compressed, 0, compressed.Length, decompressed, 0, decompressedSize);
                    if (resultSize != decompressedSize)
                        throw new InvalidDataException($"LZ4 decompression size mismatch: expected {decompressedSize}, got {resultSize}");
                    return decompressed;
                }

            case LSLibCompressionMethod.Zstd:
                {
                    using var compressedStream = new MemoryStream(compressed);
                    using var decompressedStream = new MemoryStream();
                    using var stream = new ZstdSharp.DecompressionStream(compressedStream);
                    stream.CopyTo(decompressedStream);
                    return decompressedStream.ToArray();
                }

            default:
                throw new InvalidDataException($"No decompressor found for: {compression}");
        }
    }

    public static byte[] Compress(byte[] uncompressed, CompressionFlags compression)
    {
        return Compress(uncompressed, compression.Method(), compression.Level());
    }

    public static byte[] Compress(byte[] uncompressed, LSLibCompressionMethod method, LSCompressionLevel level, bool chunked = false)
    {
        return method switch
        {
            LSLibCompressionMethod.None => uncompressed,
            LSLibCompressionMethod.Zlib => CompressZlib(uncompressed, level),
            LSLibCompressionMethod.LZ4 => CompressLZ4(uncompressed, level, chunked),
            LSLibCompressionMethod.Zstd => CompressZstd(uncompressed, level),
            _ => throw new ArgumentException("Invalid compression method")
        };
    }

    private static byte[] CompressZlib(byte[] uncompressed, LSCompressionLevel level)
    {
        var zLevel = level switch
        {
            LSCompressionLevel.Fast => CompressionLevel.Fastest,
            LSCompressionLevel.Default => CompressionLevel.Optimal,
            LSCompressionLevel.Max => CompressionLevel.SmallestSize,
            _ => throw new ArgumentException()
        };

        using var outputStream = new MemoryStream();
        using (var compressor = new ZLibStream(outputStream, zLevel, true))
        {
            compressor.Write(uncompressed, 0, uncompressed.Length);
        }
        return outputStream.ToArray();
    }

    private static byte[] CompressLZ4(byte[] uncompressed, LSCompressionLevel compressionLevel, bool chunked)
    {
        var level = compressionLevel switch
        {
            LSCompressionLevel.Fast => LZ4Level.L00_FAST,
            LSCompressionLevel.Default => LZ4Level.L10_OPT,
            LSCompressionLevel.Max => LZ4Level.L12_MAX,
            _ => throw new ArgumentException()
        };

        if (chunked)
        {
            using var output = new MemoryStream();
            using (var compressor = LZ4Stream.Encode(output, level))
            {
                compressor.Write(uncompressed, 0, uncompressed.Length);
            }
            return output.ToArray();
        }
        else
        {
            var compressed = new byte[LZ4Codec.MaximumOutputSize(uncompressed.Length)];
            int length = LZ4Codec.Encode(uncompressed, compressed, level);
            if (length < 0)
                throw new Exception($"LZ4 compression failed: {length}");
            var final = new byte[length];
            Array.Copy(compressed, final, length);
            return final;
        }
    }

    private static byte[] CompressZstd(byte[] uncompressed, LSCompressionLevel level)
    {
        var zLevel = level switch
        {
            LSCompressionLevel.Fast => 3,
            LSCompressionLevel.Default => 9,
            LSCompressionLevel.Max => 22,
            _ => throw new ArgumentException()
        };

        using var outputStream = new MemoryStream();
        using (var compressor = new ZstdSharp.CompressionStream(outputStream, zLevel, 0, true))
        {
            compressor.Write(uncompressed, 0, uncompressed.Length);
        }
        return outputStream.ToArray();
    }
}
