using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ParaTool.Core.Textures;

namespace ParaTool.App.Converters;

/// <summary>
/// Converts DDS file data to Avalonia WriteableBitmap for display in the UI.
/// </summary>
public static class DdsBitmapConverter
{
    /// <summary>
    /// Decodes a DDS file and returns an Avalonia WriteableBitmap.
    /// Returns null if the format is unsupported or decoding fails.
    /// </summary>
    public static WriteableBitmap? ToAvaloniaBitmap(byte[] ddsData)
    {
        try
        {
            var (width, height, rgba) = DdsReader.Decode(ddsData);

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormats.Rgba8888,
                AlphaFormat.Unpremul);

            using (var fb = bitmap.Lock())
            {
                var stride = fb.RowBytes;
                if (stride == width * 4)
                {
                    Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
                }
                else
                {
                    // Handle stride padding
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(rgba, y * width * 4,
                            fb.Address + y * stride, width * 4);
                    }
                }
            }

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads only the DDS header to check format and dimensions without full decode.
    /// </summary>
    public static (int width, int height, DdsFormat format)? ReadInfo(byte[] ddsData)
    {
        try
        {
            var header = DdsReader.ReadHeader(ddsData);
            return (header.Width, header.Height, header.Format);
        }
        catch
        {
            return null;
        }
    }
}
