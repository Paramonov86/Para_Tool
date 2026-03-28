namespace ParaTool.Core.Textures;

/// <summary>
/// Converts a PNG icon to BG3-compatible DDS files.
/// Generates all required variants:
/// - 380×380 DDS BC3 (main tooltip icon)
/// - 144×144 DDS BC3 (controller UI / console icon)
/// - 144×144 RGBA for atlas embedding
/// </summary>
public static class IconConverter
{
    public const int MainIconSize = 380;
    public const int ConsoleIconSize = 144;

    /// <summary>
    /// Result of converting a single PNG to BG3 icon formats.
    /// </summary>
    public sealed class IconSet
    {
        /// <summary>380×380 DDS BC3 — for Tooltips/ItemIcons/</summary>
        public required byte[] MainDds { get; init; }

        /// <summary>144×144 DDS BC3 — for ControllerUIIcons/items_png/</summary>
        public required byte[] ConsoleDds { get; init; }

        /// <summary>144×144 RGBA raw pixels — for embedding into atlas texture</summary>
        public required byte[] AtlasRgba { get; init; }

        /// <summary>Source image width before conversion</summary>
        public int SourceWidth { get; init; }

        /// <summary>Source image height before conversion</summary>
        public int SourceHeight { get; init; }
    }

    /// <summary>
    /// Convert a PNG file to a full BG3 icon set.
    /// </summary>
    public static IconSet ConvertPng(byte[] pngData)
    {
        var (srcW, srcH, rgba) = PngReader.Decode(pngData);
        return ConvertRgba(rgba, srcW, srcH);
    }

    /// <summary>
    /// Convert RGBA pixel data to a full BG3 icon set.
    /// </summary>
    public static IconSet ConvertRgba(byte[] rgba, int width, int height)
    {
        // Resize to 380×380 for main icon
        byte[] main380;
        if (width == MainIconSize && height == MainIconSize)
            main380 = rgba;
        else
            main380 = DdsWriter.ResizeRgba(rgba, width, height, MainIconSize, MainIconSize);

        // Resize to 144×144 for console icon and atlas
        byte[] console144;
        if (width == ConsoleIconSize && height == ConsoleIconSize)
            console144 = rgba;
        else
            console144 = DdsWriter.ResizeRgba(rgba, width, height, ConsoleIconSize, ConsoleIconSize);

        return new IconSet
        {
            MainDds = DdsWriter.Encode(main380, MainIconSize, MainIconSize),
            ConsoleDds = DdsWriter.Encode(console144, ConsoleIconSize, ConsoleIconSize),
            AtlasRgba = console144,
            SourceWidth = width,
            SourceHeight = height
        };
    }

    /// <summary>
    /// Build a texture atlas from multiple 144×144 RGBA icons.
    /// Returns the atlas as a DDS BC3 file and the UV coordinates for each icon.
    /// Grid layout: N×N where N = ceil(sqrt(count)), each cell = 144×144.
    /// Atlas size is always a multiple of 144 (e.g., 1440×1440 for 100 icons).
    /// </summary>
    public static (byte[] atlasDds, List<AtlasEntry> entries) BuildAtlas(
        IReadOnlyList<(string name, byte[] rgba144)> icons)
    {
        if (icons.Count == 0)
            throw new ArgumentException("No icons to build atlas from");

        int gridSize = (int)Math.Ceiling(Math.Sqrt(icons.Count));
        int atlasSize = gridSize * ConsoleIconSize;

        // Composite all icons into one big RGBA buffer
        var atlasRgba = new byte[atlasSize * atlasSize * 4];
        var entries = new List<AtlasEntry>(icons.Count);

        for (int i = 0; i < icons.Count; i++)
        {
            int gx = i % gridSize;
            int gy = i / gridSize;
            int px = gx * ConsoleIconSize;
            int py = gy * ConsoleIconSize;

            var icon = icons[i];

            // Copy icon RGBA into atlas at (px, py)
            for (int row = 0; row < ConsoleIconSize; row++)
            {
                int srcOff = row * ConsoleIconSize * 4;
                int dstOff = ((py + row) * atlasSize + px) * 4;
                Buffer.BlockCopy(icon.rgba144, srcOff, atlasRgba, dstOff, ConsoleIconSize * 4);
            }

            // UV coordinates (normalized 0..1)
            float u1 = (float)px / atlasSize;
            float v1 = (float)py / atlasSize;
            float u2 = (float)(px + ConsoleIconSize) / atlasSize;
            float v2 = (float)(py + ConsoleIconSize) / atlasSize;

            entries.Add(new AtlasEntry
            {
                Name = icon.name,
                U1 = u1, V1 = v1,
                U2 = u2, V2 = v2
            });
        }

        var atlasDds = DdsWriter.Encode(atlasRgba, atlasSize, atlasSize);
        return (atlasDds, entries);
    }

    /// <summary>
    /// Generate the LSX content for a texture atlas definition.
    /// </summary>
    public static string GenerateAtlasLsx(
        string atlasPath, string atlasUuid, int atlasSize,
        IReadOnlyList<AtlasEntry> entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<save>");
        sb.AppendLine("\t<version major=\"4\" minor=\"0\" revision=\"6\" build=\"5\" />");
        sb.AppendLine("\t<region id=\"TextureAtlasInfo\">");
        sb.AppendLine("\t\t<node id=\"root\">");
        sb.AppendLine("\t\t\t<children>");
        sb.AppendLine("\t\t\t\t<node id=\"TextureAtlasIconSize\">");
        sb.AppendLine($"\t\t\t\t\t<attribute id=\"Height\" type=\"int32\" value=\"{ConsoleIconSize}\"/>");
        sb.AppendLine($"\t\t\t\t\t<attribute id=\"Width\" type=\"int32\" value=\"{ConsoleIconSize}\"/>");
        sb.AppendLine("\t\t\t\t</node>");
        sb.AppendLine("\t\t\t\t<node id=\"TextureAtlasPath\">");
        sb.AppendLine($"\t\t\t\t\t<attribute id=\"Path\" type=\"string\" value=\"{atlasPath}\"/>");
        sb.AppendLine($"\t\t\t\t\t<attribute id=\"UUID\" type=\"FixedString\" value=\"{atlasUuid}\"/>");
        sb.AppendLine("\t\t\t\t</node>");
        sb.AppendLine("\t\t\t\t<node id=\"TextureAtlasTextureSize\">");
        sb.AppendLine($"\t\t\t\t\t<attribute id=\"Height\" type=\"int32\" value=\"{atlasSize}\"/>");
        sb.AppendLine($"\t\t\t\t\t<attribute id=\"Width\" type=\"int32\" value=\"{atlasSize}\"/>");
        sb.AppendLine("\t\t\t\t</node>");
        sb.AppendLine("\t\t\t</children>");
        sb.AppendLine("\t\t</node>");
        sb.AppendLine("\t</region>");
        sb.AppendLine("\t<region id=\"IconUVList\">");
        sb.AppendLine("\t\t<node id=\"root\">");
        sb.AppendLine("\t\t\t<children>");

        foreach (var entry in entries)
        {
            sb.AppendLine("\t\t\t\t<node id=\"IconUV\">");
            sb.AppendLine($"\t\t\t\t\t<attribute id=\"MapKey\" type=\"FixedString\" value=\"{entry.Name}\"/>");
            sb.AppendLine($"\t\t\t\t\t<attribute id=\"U1\" type=\"float\" value=\"{entry.U1}\"/>");
            sb.AppendLine($"\t\t\t\t\t<attribute id=\"U2\" type=\"float\" value=\"{entry.U2}\"/>");
            sb.AppendLine($"\t\t\t\t\t<attribute id=\"V1\" type=\"float\" value=\"{entry.V1}\"/>");
            sb.AppendLine($"\t\t\t\t\t<attribute id=\"V2\" type=\"float\" value=\"{entry.V2}\"/>");
            sb.AppendLine("\t\t\t\t</node>");
        }

        sb.AppendLine("\t\t\t</children>");
        sb.AppendLine("\t\t</node>");
        sb.AppendLine("\t</region>");
        sb.AppendLine("</save>");

        return sb.ToString();
    }
}

public sealed class AtlasEntry
{
    public string Name { get; set; } = "";
    public float U1 { get; set; }
    public float V1 { get; set; }
    public float U2 { get; set; }
    public float V2 { get; set; }
}
