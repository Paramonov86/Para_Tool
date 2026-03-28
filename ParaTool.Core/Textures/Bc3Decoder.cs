namespace ParaTool.Core.Textures;

public static class Bc3Decoder
{
    public static byte[] Decode(ReadOnlySpan<byte> blockData, int width, int height)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        var output = new byte[width * height * 4];

        // Pre-allocate outside loops to avoid StackOverflow on large textures
        Span<byte> alphas = stackalloc byte[16];
        Span<byte> colors = stackalloc byte[16 * 4];

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                int blockIndex = (by * blocksX + bx) * 16;
                if (blockIndex + 16 > blockData.Length) break;

                var block = blockData.Slice(blockIndex, 16);

                DecodeAlphaBlock(block.Slice(0, 8), alphas);
                DecodeColorBlock(block.Slice(8, 8), colors);

                // Write pixels to output
                for (int py = 0; py < 4; py++)
                {
                    int oy = by * 4 + py;
                    if (oy >= height) break;
                    for (int px = 0; px < 4; px++)
                    {
                        int ox = bx * 4 + px;
                        if (ox >= width) break;

                        int pi = py * 4 + px;
                        int oi = (oy * width + ox) * 4;
                        output[oi + 0] = colors[pi * 4 + 0]; // R
                        output[oi + 1] = colors[pi * 4 + 1]; // G
                        output[oi + 2] = colors[pi * 4 + 2]; // B
                        output[oi + 3] = alphas[pi];          // A
                    }
                }
            }
        }

        return output;
    }

    private static void DecodeAlphaBlock(ReadOnlySpan<byte> src, Span<byte> alphas)
    {
        byte alpha0 = src[0];
        byte alpha1 = src[1];

        // Build alpha lookup table
        Span<byte> alphaTable = stackalloc byte[8];
        alphaTable[0] = alpha0;
        alphaTable[1] = alpha1;

        if (alpha0 > alpha1)
        {
            alphaTable[2] = (byte)((6 * alpha0 + 1 * alpha1 + 3) / 7);
            alphaTable[3] = (byte)((5 * alpha0 + 2 * alpha1 + 3) / 7);
            alphaTable[4] = (byte)((4 * alpha0 + 3 * alpha1 + 3) / 7);
            alphaTable[5] = (byte)((3 * alpha0 + 4 * alpha1 + 3) / 7);
            alphaTable[6] = (byte)((2 * alpha0 + 5 * alpha1 + 3) / 7);
            alphaTable[7] = (byte)((1 * alpha0 + 6 * alpha1 + 3) / 7);
        }
        else
        {
            alphaTable[2] = (byte)((4 * alpha0 + 1 * alpha1 + 2) / 5);
            alphaTable[3] = (byte)((3 * alpha0 + 2 * alpha1 + 2) / 5);
            alphaTable[4] = (byte)((2 * alpha0 + 3 * alpha1 + 2) / 5);
            alphaTable[5] = (byte)((1 * alpha0 + 4 * alpha1 + 2) / 5);
            alphaTable[6] = 0;
            alphaTable[7] = 255;
        }

        // 48-bit index table: 16 pixels × 3 bits, packed in 6 bytes (src[2..7])
        // Extract as a 64-bit value for easier bit manipulation
        ulong bits = 0;
        for (int i = 0; i < 6; i++)
            bits |= (ulong)src[2 + i] << (8 * i);

        for (int i = 0; i < 16; i++)
        {
            int index = (int)((bits >> (3 * i)) & 0x7);
            alphas[i] = alphaTable[index];
        }
    }

    private static void DecodeColorBlock(ReadOnlySpan<byte> src, Span<byte> colors)
    {
        // Two RGB565 reference colors
        ushort c0 = (ushort)(src[0] | (src[1] << 8));
        ushort c1 = (ushort)(src[2] | (src[3] << 8));

        // Expand RGB565 to RGB888
        Span<byte> palette = stackalloc byte[4 * 3]; // 4 colors × RGB
        ExpandRgb565(c0, palette.Slice(0, 3));
        ExpandRgb565(c1, palette.Slice(3, 3));

        // Interpolate colors 2 and 3
        // BC3 always uses 4-color mode (unlike BC1 which has a transparent mode)
        for (int ch = 0; ch < 3; ch++)
        {
            palette[6 + ch] = (byte)((2 * palette[0 + ch] + palette[3 + ch] + 1) / 3);
            palette[9 + ch] = (byte)((palette[0 + ch] + 2 * palette[3 + ch] + 1) / 3);
        }

        // 32-bit index table: 16 pixels × 2 bits
        uint indices = (uint)(src[4] | (src[5] << 8) | (src[6] << 16) | (src[7] << 24));

        for (int i = 0; i < 16; i++)
        {
            int index = (int)((indices >> (2 * i)) & 0x3);
            colors[i * 4 + 0] = palette[index * 3 + 0]; // R
            colors[i * 4 + 1] = palette[index * 3 + 1]; // G
            colors[i * 4 + 2] = palette[index * 3 + 2]; // B
            colors[i * 4 + 3] = 255; // Alpha placeholder (overwritten by alpha block)
        }
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
