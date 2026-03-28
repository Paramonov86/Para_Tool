using System.Text;

namespace ParaTool.Core.Textures;

public enum DdsFormat
{
    Unknown,
    BC1,
    BC2,
    BC3,
    BC4,
    BC5,
    BC7,
    B8G8R8A8,
    R8G8B8A8
}

public readonly struct DdsHeader
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int MipMapCount { get; init; }
    public DdsFormat Format { get; init; }
    public int DataOffset { get; init; }

    private const uint DdsMagic = 0x20534444; // "DDS "

    public static DdsHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 128)
            throw new InvalidDataException("DDS file too small for header");

        uint magic = BitConverter.ToUInt32(data);
        if (magic != DdsMagic)
            throw new InvalidDataException($"Invalid DDS magic: 0x{magic:X8}");

        int height = BitConverter.ToInt32(data.Slice(12));
        int width = BitConverter.ToInt32(data.Slice(16));
        int mipCount = BitConverter.ToInt32(data.Slice(28));
        if (mipCount == 0) mipCount = 1;

        // Pixel format starts at offset 76
        uint pfFlags = BitConverter.ToUInt32(data.Slice(80));
        uint fourCC = BitConverter.ToUInt32(data.Slice(84));

        DdsFormat format;
        int dataOffset = 128;

        // Check FourCC
        string fourCCStr = Encoding.ASCII.GetString(data.Slice(84, 4));

        if (fourCCStr == "DXT1")
            format = DdsFormat.BC1;
        else if (fourCCStr == "DXT3")
            format = DdsFormat.BC2;
        else if (fourCCStr == "DXT5")
            format = DdsFormat.BC3;
        else if (fourCCStr == "DX10")
        {
            // DX10 extended header (20 bytes after main header)
            if (data.Length < 148)
                throw new InvalidDataException("DDS file too small for DX10 header");

            dataOffset = 148;
            uint dxgiFormat = BitConverter.ToUInt32(data.Slice(128));
            format = dxgiFormat switch
            {
                70 or 71 or 72 => DdsFormat.BC1,   // DXGI_FORMAT_BC1_TYPELESS/UNORM/SRGB
                73 or 74 or 75 => DdsFormat.BC2,   // DXGI_FORMAT_BC2_*
                76 or 77 or 78 => DdsFormat.BC3,   // DXGI_FORMAT_BC3_*
                79 or 80 or 81 => DdsFormat.BC4,   // DXGI_FORMAT_BC4_*
                82 or 83 or 84 => DdsFormat.BC5,   // DXGI_FORMAT_BC5_*
                97 or 98 or 99 => DdsFormat.BC7,   // DXGI_FORMAT_BC7_*
                87 => DdsFormat.B8G8R8A8,           // DXGI_FORMAT_B8G8R8A8_UNORM
                28 => DdsFormat.R8G8B8A8,           // DXGI_FORMAT_R8G8B8A8_UNORM
                _ => DdsFormat.Unknown
            };
        }
        else if ((pfFlags & 0x40) != 0) // DDPF_RGB
        {
            uint rgbBitCount = BitConverter.ToUInt32(data.Slice(88));
            if (rgbBitCount == 32)
                format = DdsFormat.B8G8R8A8;
            else
                format = DdsFormat.Unknown;
        }
        else
        {
            format = DdsFormat.Unknown;
        }

        return new DdsHeader
        {
            Width = width,
            Height = height,
            MipMapCount = mipCount,
            Format = format,
            DataOffset = dataOffset
        };
    }
}
