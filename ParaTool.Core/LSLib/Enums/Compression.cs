namespace ParaTool.Core.LSLib;

public enum LSLibCompressionMethod
{
    None,
    Zlib,
    LZ4,
    Zstd
}

public enum LSCompressionLevel
{
    Fast,
    Default,
    Max
}

public enum CompressionFlags : byte
{
    MethodNone = 0,
    MethodZlib = 1,
    MethodLZ4 = 2,
    MethodZstd = 3,
    FastCompress = 0x10,
    DefaultCompress = 0x20,
    MaxCompress = 0x40
}

public static class CompressionFlagExtensions
{
    public static LSLibCompressionMethod Method(this CompressionFlags f)
    {
        return (CompressionFlags)((byte)f & 0x0F) switch
        {
            CompressionFlags.MethodNone => LSLibCompressionMethod.None,
            CompressionFlags.MethodZlib => LSLibCompressionMethod.Zlib,
            CompressionFlags.MethodLZ4 => LSLibCompressionMethod.LZ4,
            CompressionFlags.MethodZstd => LSLibCompressionMethod.Zstd,
            _ => throw new NotSupportedException($"Unsupported compression method: {(byte)f & 0x0F}")
        };
    }

    public static LSCompressionLevel Level(this CompressionFlags f)
    {
        return (CompressionFlags)((byte)f & 0xF0) switch
        {
            CompressionFlags.FastCompress => LSCompressionLevel.Fast,
            CompressionFlags.DefaultCompress => LSCompressionLevel.Default,
            CompressionFlags.MaxCompress => LSCompressionLevel.Max,
            _ => LSCompressionLevel.Default
        };
    }

    public static CompressionFlags ToFlags(this LSLibCompressionMethod method)
    {
        return method switch
        {
            LSLibCompressionMethod.None => CompressionFlags.MethodNone,
            LSLibCompressionMethod.Zlib => CompressionFlags.MethodZlib,
            LSLibCompressionMethod.LZ4 => CompressionFlags.MethodLZ4,
            LSLibCompressionMethod.Zstd => CompressionFlags.MethodZstd,
            _ => throw new NotSupportedException($"Unsupported compression method: {method}")
        };
    }

    public static CompressionFlags ToFlags(this LSCompressionLevel level)
    {
        return level switch
        {
            LSCompressionLevel.Fast => CompressionFlags.FastCompress,
            LSCompressionLevel.Default => CompressionFlags.DefaultCompress,
            LSCompressionLevel.Max => CompressionFlags.MaxCompress,
            _ => throw new NotSupportedException($"Unsupported compression level: {level}")
        };
    }
}
