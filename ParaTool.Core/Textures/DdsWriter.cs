using System.Text;

namespace ParaTool.Core.Textures;

/// <summary>
/// Creates DDS files from RGBA pixel data, encoding to BC3 (DXT5).
/// Generates mipmaps automatically.
/// </summary>
public static class DdsWriter
{
    /// <summary>
    /// Encode RGBA pixels into a DDS BC3 file with mipmaps.
    /// </summary>
    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException($"RGBA data size mismatch: expected {width * height * 4}, got {rgba.Length}");

        int mipCount = CountMips(width, height);
        var blocks = new List<byte[]>();
        int totalBlockSize = 0;

        // Generate and encode each mip level
        var currentRgba = rgba;
        int w = width, h = height;

        for (int mip = 0; mip < mipCount; mip++)
        {
            var encoded = Bc3Encoder.Encode(currentRgba, w, h);
            blocks.Add(encoded);
            totalBlockSize += encoded.Length;

            if (mip < mipCount - 1)
            {
                int nw = Math.Max(w / 2, 1);
                int nh = Math.Max(h / 2, 1);
                currentRgba = DownscaleRgba(currentRgba, w, h, nw, nh);
                w = nw;
                h = nh;
            }
        }

        // Build DDS file
        var dds = new byte[128 + totalBlockSize];
        WriteDdsHeader(dds, width, height, mipCount);

        int offset = 128;
        foreach (var block in blocks)
        {
            Buffer.BlockCopy(block, 0, dds, offset, block.Length);
            offset += block.Length;
        }

        return dds;
    }

    /// <summary>
    /// Convert a PNG file (loaded as Avalonia bitmap or raw RGBA) to DDS BC3.
    /// Resizes to target dimensions if needed.
    /// </summary>
    public static byte[] EncodeResized(byte[] rgba, int srcWidth, int srcHeight, int targetWidth, int targetHeight)
    {
        byte[] resized;
        if (srcWidth == targetWidth && srcHeight == targetHeight)
            resized = rgba;
        else
            resized = ResizeRgba(rgba, srcWidth, srcHeight, targetWidth, targetHeight);

        return Encode(resized, targetWidth, targetHeight);
    }

    private static void WriteDdsHeader(byte[] dds, int width, int height, int mipCount)
    {
        // Magic
        Encoding.ASCII.GetBytes("DDS ", dds);

        // DDS_HEADER
        WriteInt(dds, 4, 124);     // dwSize
        WriteInt(dds, 8, 0x000A1007); // dwFlags: CAPS|HEIGHT|WIDTH|PIXELFORMAT|MIPMAPCOUNT|LINEARSIZE
        WriteInt(dds, 12, height);
        WriteInt(dds, 16, width);

        // Pitch/LinearSize for BC3: ((width+3)/4) * 16
        int blocksX = (width + 3) / 4;
        WriteInt(dds, 20, blocksX * 16);

        WriteInt(dds, 24, 0);      // dwDepth
        WriteInt(dds, 28, mipCount);

        // DDS_PIXELFORMAT at offset 76
        WriteInt(dds, 76, 32);      // dwSize
        WriteInt(dds, 80, 0x4);     // dwFlags: DDPF_FOURCC
        dds[84] = (byte)'D'; dds[85] = (byte)'X'; dds[86] = (byte)'T'; dds[87] = (byte)'5';

        // dwCaps
        WriteInt(dds, 108, 0x401008); // COMPLEX|TEXTURE|MIPMAP
    }

    private static void WriteInt(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value & 0xFF);
        data[offset + 1] = (byte)((value >> 8) & 0xFF);
        data[offset + 2] = (byte)((value >> 16) & 0xFF);
        data[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static int CountMips(int width, int height)
    {
        int count = 1;
        while (width > 1 || height > 1)
        {
            width = Math.Max(width / 2, 1);
            height = Math.Max(height / 2, 1);
            count++;
        }
        return count;
    }

    /// <summary>
    /// Simple box-filter downscale by 2x.
    /// </summary>
    private static byte[] DownscaleRgba(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new byte[dstW * dstH * 4];
        for (int y = 0; y < dstH; y++)
        {
            int sy = y * 2;
            int sy2 = Math.Min(sy + 1, srcH - 1);
            for (int x = 0; x < dstW; x++)
            {
                int sx = x * 2;
                int sx2 = Math.Min(sx + 1, srcW - 1);

                int i00 = (sy * srcW + sx) * 4;
                int i10 = (sy * srcW + sx2) * 4;
                int i01 = (sy2 * srcW + sx) * 4;
                int i11 = (sy2 * srcW + sx2) * 4;

                int di = (y * dstW + x) * 4;
                for (int c = 0; c < 4; c++)
                    dst[di + c] = (byte)((src[i00 + c] + src[i10 + c] + src[i01 + c] + src[i11 + c] + 2) / 4);
            }
        }
        return dst;
    }

    /// <summary>
    /// Bilinear resize to arbitrary dimensions.
    /// </summary>
    public static byte[] ResizeRgba(byte[] src, int srcW, int srcH, int dstW, int dstH)
    {
        var dst = new byte[dstW * dstH * 4];
        float scaleX = (float)srcW / dstW;
        float scaleY = (float)srcH / dstH;

        for (int y = 0; y < dstH; y++)
        {
            float fy = (y + 0.5f) * scaleY - 0.5f;
            int iy = (int)MathF.Floor(fy);
            float vy = fy - iy;

            int y0 = Math.Clamp(iy, 0, srcH - 1);
            int y1 = Math.Clamp(iy + 1, 0, srcH - 1);

            for (int x = 0; x < dstW; x++)
            {
                float fx = (x + 0.5f) * scaleX - 0.5f;
                int ix = (int)MathF.Floor(fx);
                float vx = fx - ix;

                int x0 = Math.Clamp(ix, 0, srcW - 1);
                int x1 = Math.Clamp(ix + 1, 0, srcW - 1);

                int i00 = (y0 * srcW + x0) * 4;
                int i10 = (y0 * srcW + x1) * 4;
                int i01 = (y1 * srcW + x0) * 4;
                int i11 = (y1 * srcW + x1) * 4;

                int di = (y * dstW + x) * 4;
                for (int c = 0; c < 4; c++)
                {
                    float v = src[i00 + c] * (1 - vx) * (1 - vy)
                            + src[i10 + c] * vx * (1 - vy)
                            + src[i01 + c] * (1 - vx) * vy
                            + src[i11 + c] * vx * vy;
                    dst[di + c] = (byte)Math.Clamp((int)(v + 0.5f), 0, 255);
                }
            }
        }
        return dst;
    }
}
