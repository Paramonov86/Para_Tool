using System.IO.Compression;

namespace ParaTool.Core.Textures;

/// <summary>
/// Minimal PNG reader that decodes a PNG file to RGBA pixel data.
/// Supports 8-bit RGBA (color type 6) and RGB (color type 2) with optional alpha.
/// No external dependencies.
/// </summary>
public static class PngReader
{
    public static (int width, int height, byte[] rgba) Decode(byte[] pngData)
    {
        using var ms = new MemoryStream(pngData);
        return Decode(ms);
    }

    public static (int width, int height, byte[] rgba) Decode(Stream stream)
    {
        using var reader = new BinaryReader(stream);

        // Validate PNG signature
        var sig = reader.ReadBytes(8);
        if (sig[0] != 0x89 || sig[1] != 0x50 || sig[2] != 0x4E || sig[3] != 0x47)
            throw new InvalidDataException("Not a valid PNG file");

        int width = 0, height = 0, bitDepth = 0, colorType = 0;
        var compressedData = new MemoryStream();

        while (stream.Position < stream.Length)
        {
            int chunkLen = ReadBigEndianInt32(reader);
            string chunkType = new string(reader.ReadChars(4));

            if (chunkType == "IHDR")
            {
                width = ReadBigEndianInt32(reader);
                height = ReadBigEndianInt32(reader);
                bitDepth = reader.ReadByte();
                colorType = reader.ReadByte();
                reader.ReadBytes(3); // compression, filter, interlace
                reader.ReadBytes(4); // CRC
            }
            else if (chunkType == "IDAT")
            {
                var data = reader.ReadBytes(chunkLen);
                compressedData.Write(data, 0, data.Length);
                reader.ReadBytes(4); // CRC
            }
            else if (chunkType == "IEND")
            {
                break;
            }
            else
            {
                reader.ReadBytes(chunkLen + 4); // data + CRC
            }
        }

        if (width == 0 || height == 0)
            throw new InvalidDataException("PNG IHDR chunk not found");

        if (bitDepth != 8)
            throw new NotSupportedException($"PNG bit depth {bitDepth} not supported (only 8-bit)");

        // Decompress IDAT data (zlib)
        compressedData.Position = 0;
        // Skip zlib header (2 bytes)
        compressedData.ReadByte();
        compressedData.ReadByte();

        using var deflate = new DeflateStream(compressedData, CompressionMode.Decompress);
        using var rawMs = new MemoryStream();
        deflate.CopyTo(rawMs);
        var raw = rawMs.ToArray();

        // Decode based on color type
        int channels = colorType switch
        {
            0 => 1, // Grayscale
            2 => 3, // RGB
            4 => 2, // Grayscale + Alpha
            6 => 4, // RGBA
            _ => throw new NotSupportedException($"PNG color type {colorType} not supported")
        };

        int stride = width * channels + 1; // +1 for filter byte
        var rgba = new byte[width * height * 4];
        var prevRow = new byte[width * channels];
        var currentRow = new byte[width * channels];

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * stride;
            byte filterType = raw[rowOffset];

            // Extract raw row data (without filter byte)
            Buffer.BlockCopy(raw, rowOffset + 1, currentRow, 0, width * channels);

            // Apply PNG filter
            ApplyFilter(filterType, currentRow, prevRow, channels);

            // Convert to RGBA
            for (int x = 0; x < width; x++)
            {
                int si = x * channels;
                int di = (y * width + x) * 4;

                switch (colorType)
                {
                    case 0: // Grayscale
                        rgba[di] = rgba[di + 1] = rgba[di + 2] = currentRow[si];
                        rgba[di + 3] = 255;
                        break;
                    case 2: // RGB
                        rgba[di] = currentRow[si];
                        rgba[di + 1] = currentRow[si + 1];
                        rgba[di + 2] = currentRow[si + 2];
                        rgba[di + 3] = 255;
                        break;
                    case 4: // Grayscale + Alpha
                        rgba[di] = rgba[di + 1] = rgba[di + 2] = currentRow[si];
                        rgba[di + 3] = currentRow[si + 1];
                        break;
                    case 6: // RGBA
                        rgba[di] = currentRow[si];
                        rgba[di + 1] = currentRow[si + 1];
                        rgba[di + 2] = currentRow[si + 2];
                        rgba[di + 3] = currentRow[si + 3];
                        break;
                }
            }

            // Current becomes previous
            Buffer.BlockCopy(currentRow, 0, prevRow, 0, currentRow.Length);
        }

        return (width, height, rgba);
    }

    private static void ApplyFilter(byte filterType, byte[] row, byte[] prevRow, int bpp)
    {
        switch (filterType)
        {
            case 0: // None
                break;
            case 1: // Sub
                for (int i = bpp; i < row.Length; i++)
                    row[i] = (byte)(row[i] + row[i - bpp]);
                break;
            case 2: // Up
                for (int i = 0; i < row.Length; i++)
                    row[i] = (byte)(row[i] + prevRow[i]);
                break;
            case 3: // Average
                for (int i = 0; i < row.Length; i++)
                {
                    int a = i >= bpp ? row[i - bpp] : 0;
                    int b = prevRow[i];
                    row[i] = (byte)(row[i] + (a + b) / 2);
                }
                break;
            case 4: // Paeth
                for (int i = 0; i < row.Length; i++)
                {
                    int a = i >= bpp ? row[i - bpp] : 0;
                    int b = prevRow[i];
                    int c = i >= bpp ? prevRow[i - bpp] : 0;
                    row[i] = (byte)(row[i] + PaethPredictor(a, b, c));
                }
                break;
        }
    }

    private static int PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }
}
