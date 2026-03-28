namespace ParaTool.Core.Textures;

/// <summary>
/// Encodes RGBA pixel data into BC3 (DXT5) compressed blocks.
/// Each 4x4 pixel block → 16 bytes (8 alpha + 8 color).
/// </summary>
public static class Bc3Encoder
{
    /// <summary>
    /// Encode RGBA pixel data to BC3 block data.
    /// Input: RGBA byte array (4 bytes per pixel, row-major), width, height.
    /// Width and height should be multiples of 4; if not, edge pixels are clamped.
    /// </summary>
    public static byte[] Encode(ReadOnlySpan<byte> rgba, int width, int height)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        var output = new byte[blocksX * blocksY * 16];

        Span<byte> blockRgba = stackalloc byte[16 * 4]; // 4x4 pixels × RGBA

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                // Extract 4x4 block with clamping
                for (int py = 0; py < 4; py++)
                {
                    int sy = Math.Min(by * 4 + py, height - 1);
                    for (int px = 0; px < 4; px++)
                    {
                        int sx = Math.Min(bx * 4 + px, width - 1);
                        int si = (sy * width + sx) * 4;
                        int di = (py * 4 + px) * 4;
                        blockRgba[di + 0] = rgba[si + 0]; // R
                        blockRgba[di + 1] = rgba[si + 1]; // G
                        blockRgba[di + 2] = rgba[si + 2]; // B
                        blockRgba[di + 3] = rgba[si + 3]; // A
                    }
                }

                int blockOffset = (by * blocksX + bx) * 16;
                EncodeAlphaBlock(blockRgba, output.AsSpan(blockOffset, 8));
                EncodeColorBlock(blockRgba, output.AsSpan(blockOffset + 8, 8));
            }
        }

        return output;
    }

    private static void EncodeAlphaBlock(ReadOnlySpan<byte> rgba, Span<byte> dst)
    {
        // Extract 16 alpha values
        Span<byte> alphas = stackalloc byte[16];
        byte minA = 255, maxA = 0;
        for (int i = 0; i < 16; i++)
        {
            byte a = rgba[i * 4 + 3];
            alphas[i] = a;
            if (a < minA) minA = a;
            if (a > maxA) maxA = a;
        }

        bool hasExtremes = false;
        for (int i = 0; i < 16; i++)
        {
            if (alphas[i] == 0 || alphas[i] == 255)
            {
                hasExtremes = true;
                break;
            }
        }

        byte alpha0, alpha1;
        Span<byte> palette = stackalloc byte[8];

        if (!hasExtremes || minA == maxA)
        {
            // 8-value interpolation mode (alpha0 > alpha1)
            alpha0 = maxA;
            alpha1 = minA;
            if (alpha0 == alpha1) alpha0 = (byte)Math.Min(alpha1 + 1, 255);

            palette[0] = alpha0;
            palette[1] = alpha1;
            for (int i = 0; i < 6; i++)
                palette[2 + i] = (byte)(((6 - i) * alpha0 + (1 + i) * alpha1 + 3) / 7);
        }
        else
        {
            // 6-value mode with 0 and 255 endpoints (alpha0 <= alpha1)
            // Find min/max excluding 0 and 255
            byte innerMin = 255, innerMax = 0;
            for (int i = 0; i < 16; i++)
            {
                byte a = alphas[i];
                if (a > 0 && a < 255)
                {
                    if (a < innerMin) innerMin = a;
                    if (a > innerMax) innerMax = a;
                }
            }

            if (innerMin > innerMax) { innerMin = 0; innerMax = 1; }

            alpha0 = innerMin;
            alpha1 = innerMax;
            if (alpha0 == alpha1) alpha1 = (byte)Math.Min(alpha0 + 1, 255);
            if (alpha0 > alpha1) (alpha0, alpha1) = (alpha1, alpha0);

            palette[0] = alpha0;
            palette[1] = alpha1;
            for (int i = 0; i < 4; i++)
                palette[2 + i] = (byte)(((4 - i) * alpha0 + (1 + i) * alpha1 + 2) / 5);
            palette[6] = 0;
            palette[7] = 255;
        }

        dst[0] = alpha0;
        dst[1] = alpha1;

        // Find closest palette index for each alpha
        ulong bits = 0;
        for (int i = 0; i < 16; i++)
        {
            int bestIdx = 0;
            int bestDist = Math.Abs(alphas[i] - palette[0]);
            for (int j = 1; j < 8; j++)
            {
                int dist = Math.Abs(alphas[i] - palette[j]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = j;
                }
            }
            bits |= (ulong)bestIdx << (3 * i);
        }

        for (int i = 0; i < 6; i++)
            dst[2 + i] = (byte)((bits >> (8 * i)) & 0xFF);
    }

    private static void EncodeColorBlock(ReadOnlySpan<byte> rgba, Span<byte> dst)
    {
        // Find bounding box of RGB values
        int minR = 255, minG = 255, minB = 255;
        int maxR = 0, maxG = 0, maxB = 0;

        for (int i = 0; i < 16; i++)
        {
            int r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2];
            if (r < minR) minR = r;
            if (g < minG) minG = g;
            if (b < minB) minB = b;
            if (r > maxR) maxR = r;
            if (g > maxG) maxG = g;
            if (b > maxB) maxB = b;
        }

        // Inset bounding box slightly for better quality
        int insetR = (maxR - minR) >> 4;
        int insetG = (maxG - minG) >> 4;
        int insetB = (maxB - minB) >> 4;
        minR = Math.Min(minR + insetR, 255);
        minG = Math.Min(minG + insetG, 255);
        minB = Math.Min(minB + insetB, 255);
        maxR = Math.Max(maxR - insetR, 0);
        maxG = Math.Max(maxG - insetG, 0);
        maxB = Math.Max(maxB - insetB, 0);

        ushort c0 = ToRgb565(maxR, maxG, maxB);
        ushort c1 = ToRgb565(minR, minG, minB);

        // Ensure c0 > c1 for 4-color mode (required for BC3)
        if (c0 < c1) (c0, c1) = (c1, c0);
        if (c0 == c1)
        {
            if (c0 < 0xFFFF) c0++;
            else c1--;
        }

        // Build 4-color palette
        Span<int> palR = stackalloc int[4];
        Span<int> palG = stackalloc int[4];
        Span<int> palB = stackalloc int[4];
        FromRgb565(c0, out palR[0], out palG[0], out palB[0]);
        FromRgb565(c1, out palR[1], out palG[1], out palB[1]);
        palR[2] = (2 * palR[0] + palR[1] + 1) / 3;
        palG[2] = (2 * palG[0] + palG[1] + 1) / 3;
        palB[2] = (2 * palB[0] + palB[1] + 1) / 3;
        palR[3] = (palR[0] + 2 * palR[1] + 1) / 3;
        palG[3] = (palG[0] + 2 * palG[1] + 1) / 3;
        palB[3] = (palB[0] + 2 * palB[1] + 1) / 3;

        // Find closest palette index for each pixel
        uint indices = 0;
        for (int i = 0; i < 16; i++)
        {
            int r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2];
            int bestIdx = 0;
            int bestDist = ColorDistSq(r, g, b, palR[0], palG[0], palB[0]);
            for (int j = 1; j < 4; j++)
            {
                int dist = ColorDistSq(r, g, b, palR[j], palG[j], palB[j]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = j;
                }
            }
            indices |= (uint)bestIdx << (2 * i);
        }

        dst[0] = (byte)(c0 & 0xFF);
        dst[1] = (byte)(c0 >> 8);
        dst[2] = (byte)(c1 & 0xFF);
        dst[3] = (byte)(c1 >> 8);
        dst[4] = (byte)(indices & 0xFF);
        dst[5] = (byte)((indices >> 8) & 0xFF);
        dst[6] = (byte)((indices >> 16) & 0xFF);
        dst[7] = (byte)((indices >> 24) & 0xFF);
    }

    private static ushort ToRgb565(int r, int g, int b)
    {
        return (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
    }

    private static void FromRgb565(ushort c, out int r, out int g, out int b)
    {
        int r5 = (c >> 11) & 0x1F;
        int g6 = (c >> 5) & 0x3F;
        int b5 = c & 0x1F;
        r = (r5 << 3) | (r5 >> 2);
        g = (g6 << 2) | (g6 >> 4);
        b = (b5 << 3) | (b5 >> 2);
    }

    private static int ColorDistSq(int r1, int g1, int b1, int r2, int g2, int b2)
    {
        int dr = r1 - r2, dg = g1 - g2, db = b1 - b2;
        return dr * dr + dg * dg + db * db;
    }
}
