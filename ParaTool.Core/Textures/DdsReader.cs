namespace ParaTool.Core.Textures;

public static class DdsReader
{
    public static DdsHeader ReadHeader(byte[] data)
    {
        return DdsHeader.Parse(data);
    }

    public static (int width, int height, byte[] rgba) Decode(byte[] data)
    {
        var header = DdsHeader.Parse(data);

        if (header.Format == DdsFormat.Unknown)
            throw new NotSupportedException("Unsupported DDS format");

        var pixelData = data.AsSpan(header.DataOffset);

        byte[] rgba = header.Format switch
        {
            DdsFormat.BC1 => DecodeBc1(pixelData, header.Width, header.Height),
            DdsFormat.BC3 => Bc3Decoder.Decode(pixelData, header.Width, header.Height),
            DdsFormat.B8G8R8A8 => ConvertBgra(pixelData, header.Width, header.Height),
            DdsFormat.R8G8B8A8 => pixelData.Slice(0, header.Width * header.Height * 4).ToArray(),
            _ => throw new NotSupportedException($"DDS format {header.Format} decoding not yet implemented")
        };

        return (header.Width, header.Height, rgba);
    }

    private static byte[] DecodeBc1(ReadOnlySpan<byte> blockData, int width, int height)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        var output = new byte[width * height * 4];

        // Pre-allocate outside loops to avoid StackOverflow
        Span<byte> palette = stackalloc byte[4 * 4];

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                int blockIndex = (by * blocksX + bx) * 8;
                if (blockIndex + 8 > blockData.Length) break;

                var block = blockData.Slice(blockIndex, 8);

                ushort c0 = (ushort)(block[0] | (block[1] << 8));
                ushort c1 = (ushort)(block[2] | (block[3] << 8));
                ExpandRgb565(c0, palette.Slice(0, 3));
                palette[3] = 255;
                ExpandRgb565(c1, palette.Slice(4, 3));
                palette[7] = 255;

                if (c0 > c1)
                {
                    for (int ch = 0; ch < 3; ch++)
                    {
                        palette[8 + ch] = (byte)((2 * palette[ch] + palette[4 + ch] + 1) / 3);
                        palette[12 + ch] = (byte)((palette[ch] + 2 * palette[4 + ch] + 1) / 3);
                    }
                    palette[11] = 255;
                    palette[15] = 255;
                }
                else
                {
                    for (int ch = 0; ch < 3; ch++)
                    {
                        palette[8 + ch] = (byte)((palette[ch] + palette[4 + ch]) / 2);
                        palette[12 + ch] = 0;
                    }
                    palette[11] = 255;
                    palette[15] = 0; // Transparent
                }

                uint indices = (uint)(block[4] | (block[5] << 8) | (block[6] << 16) | (block[7] << 24));

                for (int py = 0; py < 4; py++)
                {
                    int oy = by * 4 + py;
                    if (oy >= height) break;
                    for (int px = 0; px < 4; px++)
                    {
                        int ox = bx * 4 + px;
                        if (ox >= width) break;

                        int pi = py * 4 + px;
                        int index = (int)((indices >> (2 * pi)) & 0x3);
                        int oi = (oy * width + ox) * 4;
                        output[oi + 0] = palette[index * 4 + 0];
                        output[oi + 1] = palette[index * 4 + 1];
                        output[oi + 2] = palette[index * 4 + 2];
                        output[oi + 3] = palette[index * 4 + 3];
                    }
                }
            }
        }

        return output;
    }

    private static byte[] ConvertBgra(ReadOnlySpan<byte> data, int width, int height)
    {
        int pixelCount = width * height;
        var rgba = new byte[pixelCount * 4];
        for (int i = 0; i < pixelCount; i++)
        {
            int si = i * 4;
            rgba[si + 0] = data[si + 2]; // R <- B
            rgba[si + 1] = data[si + 1]; // G
            rgba[si + 2] = data[si + 0]; // B <- R
            rgba[si + 3] = data[si + 3]; // A
        }
        return rgba;
    }

    private static void ExpandRgb565(ushort color, Span<byte> rgb)
    {
        int r5 = (color >> 11) & 0x1F;
        int g6 = (color >> 5) & 0x3F;
        int b5 = color & 0x1F;
        rgb[0] = (byte)((r5 << 3) | (r5 >> 2));
        rgb[1] = (byte)((g6 << 2) | (g6 >> 4));
        rgb[2] = (byte)((b5 << 3) | (b5 >> 2));
    }
}
