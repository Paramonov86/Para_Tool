namespace ParaTool.Core.Textures;

/// <summary>
/// BC7 (BPTC) decoder. The game ships its tooltip icons (Tooltips/Icons, ItemIcons) as BC7; the
/// atlases are BC3. Follows the BC7 format specification: eight modes, 2- and 3-subset
/// partitions with their anchor indices, p-bits, and channel rotation for modes 4 and 5.
/// </summary>
public static class Bc7Decoder
{
    private static readonly int[] Subsets = [3, 2, 3, 2, 1, 1, 1, 2];
    private static readonly int[] PartitionBits = [4, 6, 6, 6, 0, 0, 0, 6];
    private static readonly int[] RotationBits = [0, 0, 0, 0, 2, 2, 0, 0];
    private static readonly int[] IndexSelectionBits = [0, 0, 0, 0, 1, 0, 0, 0];
    private static readonly int[] ColorBits = [4, 6, 5, 7, 5, 7, 7, 5];
    private static readonly int[] AlphaBits = [0, 0, 0, 0, 6, 8, 7, 5];
    private static readonly bool[] EndpointPBits = [true, false, false, true, false, false, true, true];
    private static readonly bool[] SharedPBits = [false, true, false, false, false, false, false, false];
    private static readonly int[] IndexBits = [3, 3, 2, 2, 2, 2, 4, 2];
    private static readonly int[] IndexBits2 = [0, 0, 0, 0, 3, 2, 0, 0];

    private static readonly int[] Weights2 = [0, 21, 43, 64];
    private static readonly int[] Weights3 = [0, 9, 18, 27, 37, 46, 55, 64];
    private static readonly int[] Weights4 = [0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64];

    internal static readonly byte[][] Partitions2 =
    [
        [0,0,1,1,0,0,1,1,0,0,1,1,0,0,1,1], [0,0,0,1,0,0,0,1,0,0,0,1,0,0,0,1],
        [0,1,1,1,0,1,1,1,0,1,1,1,0,1,1,1], [0,0,0,1,0,0,1,1,0,0,1,1,0,1,1,1],
        [0,0,0,0,0,0,0,1,0,0,0,1,0,0,1,1], [0,0,1,1,0,1,1,1,0,1,1,1,1,1,1,1],
        [0,0,0,1,0,0,1,1,0,1,1,1,1,1,1,1], [0,0,0,0,0,0,0,1,0,0,1,1,0,1,1,1],
        [0,0,0,0,0,0,0,0,0,0,0,1,0,0,1,1], [0,0,1,1,0,1,1,1,1,1,1,1,1,1,1,1],
        [0,0,0,0,0,0,0,1,0,1,1,1,1,1,1,1], [0,0,0,0,0,0,0,0,0,0,0,1,0,1,1,1],
        [0,0,0,1,0,1,1,1,1,1,1,1,1,1,1,1], [0,0,0,0,0,0,0,0,1,1,1,1,1,1,1,1],
        [0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1], [0,0,0,0,0,0,0,0,0,0,0,0,1,1,1,1],
        [0,0,0,0,1,0,0,0,1,1,1,0,1,1,1,1], [0,1,1,1,0,0,0,1,0,0,0,0,0,0,0,0],
        [0,0,0,0,0,0,0,0,1,0,0,0,1,1,1,0], [0,1,1,1,0,0,1,1,0,0,0,1,0,0,0,0],
        [0,0,1,1,0,0,0,1,0,0,0,0,0,0,0,0], [0,0,0,0,1,0,0,0,1,1,0,0,1,1,1,0],
        [0,0,0,0,0,0,0,0,1,0,0,0,1,1,0,0], [0,1,1,1,0,0,1,1,0,0,1,1,0,0,0,1],
        [0,0,1,1,0,0,0,1,0,0,0,1,0,0,0,0], [0,0,0,0,1,0,0,0,1,0,0,0,1,1,0,0],
        [0,1,1,0,0,1,1,0,0,1,1,0,0,1,1,0], [0,0,1,1,0,1,1,0,0,1,1,0,1,1,0,0],
        [0,0,0,1,0,1,1,1,1,1,1,0,1,0,0,0], [0,0,0,0,1,1,1,1,1,1,1,1,0,0,0,0],
        [0,1,1,1,0,0,0,1,1,0,0,0,1,1,1,0], [0,0,1,1,1,0,0,1,1,0,0,1,1,1,0,0],
        [0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1], [0,0,0,0,1,1,1,1,0,0,0,0,1,1,1,1],
        [0,1,0,1,1,0,1,0,0,1,0,1,1,0,1,0], [0,0,1,1,0,0,1,1,1,1,0,0,1,1,0,0],
        [0,0,1,1,1,1,0,0,0,0,1,1,1,1,0,0], [0,1,0,1,0,1,0,1,1,0,1,0,1,0,1,0],
        [0,1,1,0,1,0,0,1,0,1,1,0,1,0,0,1], [0,1,0,1,1,0,1,0,1,0,1,0,0,1,0,1],
        [0,1,1,1,0,0,1,1,1,1,0,0,1,1,1,0], [0,0,0,1,0,0,1,1,1,1,0,0,1,0,0,0],
        [0,0,1,1,0,0,1,0,0,1,0,0,1,1,0,0], [0,0,1,1,1,0,1,1,1,1,0,1,1,1,0,0],
        [0,1,1,0,1,0,0,1,1,0,0,1,0,1,1,0], [0,0,1,1,1,1,0,0,1,1,0,0,0,0,1,1],
        [0,1,1,0,0,1,1,0,1,0,0,1,1,0,0,1], [0,0,0,0,0,1,1,0,0,1,1,0,0,0,0,0],
        [0,1,0,0,1,1,1,0,0,1,0,0,0,0,0,0], [0,0,1,0,0,1,1,1,0,0,1,0,0,0,0,0],
        [0,0,0,0,0,0,1,0,0,1,1,1,0,0,1,0], [0,0,0,0,0,1,0,0,1,1,1,0,0,1,0,0],
        [0,1,1,0,1,1,0,0,1,0,0,1,0,0,1,1], [0,0,1,1,0,1,1,0,1,1,0,0,1,0,0,1],
        [0,1,1,0,0,0,1,1,1,0,0,1,1,1,0,0], [0,0,1,1,1,0,0,1,1,1,0,0,0,1,1,0],
        [0,1,1,0,1,1,0,0,1,1,0,0,1,0,0,1], [0,1,1,0,0,0,1,1,0,0,1,1,1,0,0,1],
        [0,1,1,1,1,1,1,0,1,0,0,0,0,0,0,1], [0,0,0,1,1,0,0,0,1,1,1,0,0,1,1,1],
        [0,0,0,0,1,1,1,1,0,0,1,1,0,0,1,1], [0,0,1,1,0,0,1,1,1,1,1,1,0,0,0,0],
        [0,0,1,0,0,0,1,0,1,1,1,0,1,1,1,0], [0,1,0,0,0,1,0,0,0,1,1,1,0,1,1,1],
    ];

    internal static readonly byte[][] Partitions3 =
    [
        [0,0,1,1,0,0,1,1,0,2,2,1,2,2,2,2], [0,0,0,1,0,0,1,1,2,2,1,1,2,2,2,1],
        [0,0,0,0,2,0,0,1,2,2,1,1,2,2,1,1], [0,2,2,2,0,0,2,2,0,0,1,1,0,1,1,1],
        [0,0,0,0,0,0,0,0,1,1,2,2,1,1,2,2], [0,0,1,1,0,0,1,1,0,0,2,2,0,0,2,2],
        [0,0,2,2,0,0,2,2,1,1,1,1,1,1,1,1], [0,0,1,1,0,0,1,1,2,2,1,1,2,2,1,1],
        [0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2], [0,0,0,0,1,1,1,1,1,1,1,1,2,2,2,2],
        [0,0,0,0,1,1,1,1,2,2,2,2,2,2,2,2], [0,0,1,2,0,0,1,2,0,0,1,2,0,0,1,2],
        [0,1,1,2,0,1,1,2,0,1,1,2,0,1,1,2], [0,1,2,2,0,1,2,2,0,1,2,2,0,1,2,2],
        [0,0,1,1,0,1,1,2,1,1,2,2,1,2,2,2], [0,0,1,1,2,0,0,1,2,2,0,0,2,2,2,0],
        [0,0,0,1,0,0,1,1,0,1,1,2,1,1,2,2], [0,1,1,1,0,0,1,1,2,0,0,1,2,2,0,0],
        [0,0,0,0,1,1,2,2,1,1,2,2,1,1,2,2], [0,0,2,2,0,0,2,2,0,0,2,2,1,1,1,1],
        [0,1,1,1,0,1,1,1,0,2,2,2,0,2,2,2], [0,0,0,1,0,0,0,1,2,2,2,1,2,2,2,1],
        [0,0,0,0,0,0,1,1,0,1,2,2,0,1,2,2], [0,0,0,0,1,1,0,0,2,2,1,0,2,2,1,0],
        [0,1,2,2,0,1,2,2,0,0,1,1,0,0,0,0], [0,0,1,2,0,0,1,2,1,1,2,2,2,2,2,2],
        [0,1,1,0,1,2,2,1,1,2,2,1,0,1,1,0], [0,0,0,0,0,1,1,0,1,2,2,1,1,2,2,1],
        [0,0,2,2,1,1,0,2,1,1,0,2,0,0,2,2], [0,1,1,0,0,1,1,0,2,0,0,2,2,2,2,2],
        [0,0,1,1,0,1,2,2,0,1,2,2,0,0,1,1], [0,0,0,0,2,0,0,0,2,2,1,1,2,2,2,1],
        [0,0,0,0,0,0,0,2,1,1,2,2,1,2,2,2], [0,2,2,2,0,0,2,2,0,0,1,2,0,0,1,1],
        [0,0,1,1,0,0,1,2,0,0,2,2,0,2,2,2], [0,1,2,0,0,1,2,0,0,1,2,0,0,1,2,0],
        [0,0,0,0,1,1,1,1,2,2,2,2,0,0,0,0], [0,1,2,0,1,2,0,1,2,0,1,2,0,1,2,0],
        [0,1,2,0,2,0,1,2,1,2,0,1,0,1,2,0], [0,0,1,1,2,2,0,0,1,1,2,2,0,0,1,1],
        [0,0,1,1,1,1,2,2,2,2,0,0,0,0,1,1], [0,1,0,1,0,1,0,1,2,2,2,2,2,2,2,2],
        [0,0,0,0,0,0,0,0,2,1,2,1,2,1,2,1], [0,0,2,2,1,1,2,2,0,0,2,2,1,1,2,2],
        [0,0,2,2,0,0,1,1,0,0,2,2,0,0,1,1], [0,2,2,0,1,2,2,1,0,2,2,0,1,2,2,1],
        [0,1,0,1,2,2,2,2,2,2,2,2,0,1,0,1], [0,0,0,0,2,1,2,1,2,1,2,1,2,1,2,1],
        [0,1,0,1,0,1,0,1,0,1,0,1,2,2,2,2], [0,2,2,2,0,1,1,1,0,2,2,2,0,1,1,1],
        [0,0,0,2,1,1,1,2,0,0,0,2,1,1,1,2], [0,0,0,0,2,1,1,2,2,1,1,2,2,1,1,2],
        [0,2,2,2,0,1,1,1,0,1,1,1,0,2,2,2], [0,0,0,2,1,1,1,2,1,1,1,2,0,0,0,2],
        [0,1,1,0,0,1,1,0,0,1,1,0,2,2,2,2], [0,0,0,0,0,0,0,0,2,1,1,2,2,1,1,2],
        [0,1,1,0,0,1,1,0,2,2,2,2,2,2,2,2], [0,0,2,2,0,0,1,1,0,0,1,1,0,0,2,2],
        [0,0,2,2,1,1,2,2,1,1,2,2,0,0,2,2], [0,0,0,0,0,0,0,0,0,0,0,0,2,1,1,2],
        [0,0,0,2,0,0,0,1,0,0,0,2,0,0,0,1], [0,2,2,2,1,2,2,2,0,2,2,2,1,2,2,2],
        [0,1,0,1,2,2,2,2,2,2,2,2,2,2,2,2], [0,1,1,1,2,0,1,1,2,2,0,1,2,2,2,0],
    ];

    /// <summary>Anchor index of the second subset, 2-subset partitions.</summary>
    internal static readonly byte[] Anchor2 =
    [
        15,15,15,15,15,15,15,15, 15,15,15,15,15,15,15,15,
        15, 2, 8, 2, 2, 8, 8,15,  2, 8, 2, 2, 8, 8, 2, 2,
        15,15, 6, 8, 2, 8,15,15,  2, 8, 2, 2, 2,15,15, 6,
         6, 2, 6, 8,15,15, 2, 2, 15,15,15,15,15, 2, 2,15,
    ];

    /// <summary>Anchor indices of the second and third subsets, 3-subset partitions.</summary>
    internal static readonly byte[] Anchor3a =
    [
         3, 3,15,15, 8, 3,15,15,  8, 8, 6, 6, 6, 5, 3, 3,
         3, 3, 8,15, 3, 3, 6,10,  5, 8, 8, 6, 8, 5,15,15,
         8,15, 3, 5, 6,10, 8,15, 15, 3,15, 5,15,15,15,15,
         3,15, 5, 5, 5, 8, 5,10,  5,10, 8,13,15,12, 3, 3,
    ];

    internal static readonly byte[] Anchor3b =
    [
        15, 8, 8, 3,15,15, 3, 8, 15,15,15,15,15,15,15, 8,
        15, 8,15, 3,15, 8,15, 8,  3,15, 6,10,15,15,10, 8,
        15, 3,15,10,10, 8, 9,10,  6,15, 8,15, 3, 6, 6, 8,
        15, 3,15,15,15,15,15,15, 15,15,15,15, 3,15,15, 8,
    ];

    public static byte[] Decode(ReadOnlySpan<byte> data, int width, int height)
    {
        int blocksX = (width + 3) / 4, blocksY = (height + 3) / 4;
        var output = new byte[width * height * 4];
        Span<byte> block = stackalloc byte[64];
        for (int by = 0; by < blocksY; by++)
        for (int bx = 0; bx < blocksX; bx++)
        {
            int offset = (by * blocksX + bx) * 16;
            if (offset + 16 > data.Length) return output;
            DecodeBlock(data.Slice(offset, 16), block);
            for (int py = 0; py < 4; py++)
            {
                int y = by * 4 + py;
                if (y >= height) break;
                for (int px = 0; px < 4; px++)
                {
                    int x = bx * 4 + px;
                    if (x >= width) break;
                    block.Slice((py * 4 + px) * 4, 4).CopyTo(output.AsSpan((y * width + x) * 4, 4));
                }
            }
        }
        return output;
    }

    private ref struct Bits
    {
        private readonly ulong _lo, _hi;
        private int _pos;
        public Bits(ReadOnlySpan<byte> b)
        {
            _lo = BitConverter.ToUInt64(b[..8]);
            _hi = BitConverter.ToUInt64(b.Slice(8, 8));
            _pos = 0;
        }
        public int Read(int count)
        {
            if (count == 0) return 0;
            ulong v;
            if (_pos >= 64) v = _hi >> (_pos - 64);
            else if (_pos + count <= 64) v = _lo >> _pos;
            else v = (_lo >> _pos) | (_hi << (64 - _pos));
            _pos += count;
            return (int)(v & ((1UL << count) - 1));
        }
    }

    /// <summary>Decodes one 16-byte block into 16 RGBA pixels, row by row.</summary>
    internal static void DecodeBlock(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        int mode = 0;
        while (mode < 8 && (src[0] & (1 << mode)) == 0) mode++;
        if (mode == 8) { dst.Clear(); return; }

        var bits = new Bits(src);
        bits.Read(mode + 1);
        int ns = Subsets[mode];
        int partition = bits.Read(PartitionBits[mode]);
        int rotation = bits.Read(RotationBits[mode]);
        int indexSelection = bits.Read(IndexSelectionBits[mode]);

        Span<int> ep = stackalloc int[6 * 4];
        int endpoints = ns * 2;
        for (int c = 0; c < 3; c++)
            for (int i = 0; i < endpoints; i++)
                ep[i * 4 + c] = bits.Read(ColorBits[mode]);
        int ab = AlphaBits[mode];
        if (ab > 0)
            for (int i = 0; i < endpoints; i++)
                ep[i * 4 + 3] = bits.Read(ab);

        int colorPrecision = ColorBits[mode], alphaPrecision = ab;
        if (EndpointPBits[mode])
        {
            for (int i = 0; i < endpoints; i++)
            {
                int p = bits.Read(1);
                for (int c = 0; c < 4; c++) ep[i * 4 + c] = (ep[i * 4 + c] << 1) | p;
            }
            colorPrecision++;
            if (ab > 0) alphaPrecision++;
        }
        else if (SharedPBits[mode])
        {
            for (int s = 0; s < ns; s++)
            {
                int p = bits.Read(1);
                for (int e = 0; e < 2; e++)
                    for (int c = 0; c < 4; c++) ep[(s * 2 + e) * 4 + c] = (ep[(s * 2 + e) * 4 + c] << 1) | p;
            }
            colorPrecision++;
        }

        for (int i = 0; i < endpoints; i++)
        {
            for (int c = 0; c < 3; c++) ep[i * 4 + c] = Expand(ep[i * 4 + c], colorPrecision);
            ep[i * 4 + 3] = ab > 0 ? Expand(ep[i * 4 + 3], alphaPrecision) : 255;
        }

        byte SubsetOf(int i) => ns switch
        {
            2 => Partitions2[partition][i],
            3 => Partitions3[partition][i],
            _ => 0,
        };
        bool IsAnchor(int i) => i == 0
            || (ns == 2 && i == Anchor2[partition])
            || (ns == 3 && (i == Anchor3a[partition] || i == Anchor3b[partition]));

        Span<int> idx = stackalloc int[16];
        Span<int> idx2 = stackalloc int[16];
        int ib = IndexBits[mode], ib2 = IndexBits2[mode];
        for (int i = 0; i < 16; i++) idx[i] = bits.Read(IsAnchor(i) ? ib - 1 : ib);
        if (ib2 > 0)
            for (int i = 0; i < 16; i++) idx2[i] = bits.Read(i == 0 ? ib2 - 1 : ib2);

        for (int i = 0; i < 16; i++)
        {
            int s = SubsetOf(i);
            int e0 = s * 2 * 4, e1 = (s * 2 + 1) * 4;
            int colorIndex, colorBits, alphaIndex, alphaBits;
            if (ib2 == 0) { colorIndex = alphaIndex = idx[i]; colorBits = alphaBits = ib; }
            else if (indexSelection == 0) { colorIndex = idx[i]; colorBits = ib; alphaIndex = idx2[i]; alphaBits = ib2; }
            else { colorIndex = idx2[i]; colorBits = ib2; alphaIndex = idx[i]; alphaBits = ib; }

            int cw = Weight(colorBits, colorIndex), aw = Weight(alphaBits, alphaIndex);
            int r = Interpolate(ep[e0], ep[e1], cw);
            int g = Interpolate(ep[e0 + 1], ep[e1 + 1], cw);
            int b = Interpolate(ep[e0 + 2], ep[e1 + 2], cw);
            int a = Interpolate(ep[e0 + 3], ep[e1 + 3], aw);
            switch (rotation)
            {
                case 1: (r, a) = (a, r); break;
                case 2: (g, a) = (a, g); break;
                case 3: (b, a) = (a, b); break;
            }
            dst[i * 4] = (byte)r; dst[i * 4 + 1] = (byte)g; dst[i * 4 + 2] = (byte)b; dst[i * 4 + 3] = (byte)a;
        }
    }

    private static int Expand(int v, int bits) => bits >= 8 ? v : (v << (8 - bits)) | (v >> (2 * bits - 8));

    private static int Weight(int bits, int index) => bits switch
    {
        2 => Weights2[index],
        3 => Weights3[index],
        _ => Weights4[index],
    };

    private static int Interpolate(int e0, int e1, int w) => ((64 - w) * e0 + w * e1 + 32) >> 6;
}
