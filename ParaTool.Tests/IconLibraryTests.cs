using ParaTool.Core;
using ParaTool.Core.Icons;
using ParaTool.Core.Textures;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// The icon library reads what a pak ships: atlases (Public/*/GUI/*.lsx + texture) for the small
/// tiles, Tooltips pictures for the large ones, and sorts every icon by what it can be given to.
/// </summary>
public class IconLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pt_icons_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private static byte[] Solid(int w, int h, byte r, byte g, byte b)
    {
        var rgba = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++) { rgba[i * 4] = r; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = b; rgba[i * 4 + 3] = 255; }
        return rgba;
    }

    private static string AtlasLsx(string texture, params (string key, float u1, float u2)[] tiles)
    {
        // Attribute order varies between the game's atlases (SharedDev lists U1 before MapKey).
        var nodes = string.Join("\n", tiles.Select(t =>
            $"<node id=\"IconUV\"><attribute id=\"U1\" type=\"float\" value=\"{t.u1.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"/>" +
            $"<attribute id=\"U2\" type=\"float\" value=\"{t.u2.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"/>" +
            "<attribute id=\"V1\" type=\"float\" value=\"0\"/><attribute id=\"V2\" type=\"float\" value=\"1\"/>" +
            $"<attribute id=\"MapKey\" type=\"FixedString\" value=\"{t.key}\"/></node>"));
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <save><region id="IconUVList"><node id="root"><children>{nodes}</children></node></region>
            <region id="TextureAtlasInfo"><node id="root"><children>
            <node id="TextureAtlasIconSize"><attribute id="Height" type="int32" value="64"/><attribute id="Width" type="int32" value="64"/></node>
            <node id="TextureAtlasPath"><attribute id="Path" type="string" value="{texture}"/><attribute id="UUID" type="FixedString" value="x"/></node>
            </children></node></region></save>
            """;
    }

    private string BuildModPak()
    {
        var src = Path.Combine(_root, "src");
        void Put(string rel, byte[] data)
        {
            var p = Path.Combine(src, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllBytes(p, data);
        }

        // A skills atlas: two 64px tiles side by side (red spell, blue status).
        var atlas = new byte[128 * 64 * 4];
        var red = Solid(64, 64, 255, 0, 0);
        var blue = Solid(64, 64, 0, 0, 255);
        for (int y = 0; y < 64; y++)
        {
            Buffer.BlockCopy(red, y * 256, atlas, y * 512, 256);
            Buffer.BlockCopy(blue, y * 256, atlas, y * 512 + 256, 256);
        }
        Put("Public/Mod/Assets/Textures/Icons/Spells.dds", DdsWriter.Encode(atlas, 128, 64));
        Put("Public/Mod/GUI/Spells.lsx", System.Text.Encoding.UTF8.GetBytes(
            AtlasLsx("Assets/Textures/Icons/Spells.dds", ("Mod_Spell", 0, 0.5f), ("Mod_Status", 0.5f, 1))));
        Put("Mods/Mod/GUI/Assets/Tooltips/Icons/Mod_Spell.DDS", DdsWriter.Encode(Solid(8, 8, 0, 255, 0), 8, 8));

        // A character creation atlas: nothing to do with spells, statuses or items.
        Put("Public/Mod/Assets/Textures/Icons/Hair.dds", DdsWriter.Encode(Solid(64, 64, 9, 9, 9), 64, 64));
        Put("Public/Mod/GUI/Hair.lsx", System.Text.Encoding.UTF8.GetBytes(AtlasLsx("Assets/Textures/Icons/Hair.dds", ("Mod_Hair", 0, 1))));

        // An item picture with no atlas tile.
        Put("Mods/Mod/GUI/Assets/Tooltips/ItemIcons/Mod_Ring.DDS", DdsWriter.Encode(Solid(8, 8, 1, 2, 3), 8, 8));

        var pak = Path.Combine(_root, "Mod.pak");
        PakWriter.CreatePak(src, pak);
        return pak;
    }

    [Fact]
    public void Build_SortsIconsByWhatTheyFit()
    {
        var lib = IconLibrary.Build(null, [new IconPakSource(BuildModPak(), "Mod")], embeddedItemAtlases: false);

        Assert.Equal(IconKind.Spell, lib.Find("Mod_Spell")!.Kind);
        Assert.True(lib.Find("Mod_Spell")!.HasLarge);
        Assert.Equal(IconKind.Status, lib.Find("Mod_Status")!.Kind);
        Assert.False(lib.Find("Mod_Status")!.HasLarge);
        Assert.Equal(IconKind.Item, lib.Find("Mod_Ring")!.Kind);
        Assert.False(lib.Find("Mod_Ring")!.HasTile);
        Assert.Null(lib.Find("Mod_Hair"));
        Assert.Equal("Mod", lib.Find("Mod_Spell")!.Source);
    }

    [Fact]
    public void Build_KeepsAnUnsortedTile_AStatsEntryUses()
    {
        var lib = IconLibrary.Build(null, [new IconPakSource(BuildModPak(), "Mod")],
            statsIcons: new HashSet<string>(["Mod_Hair"], StringComparer.OrdinalIgnoreCase), embeddedItemAtlases: false);
        Assert.Equal(IconKind.Status, lib.Find("Mod_Hair")!.Kind);
    }

    [Fact]
    public void Pixels_TileThumbnailAndLarge()
    {
        var lib = IconLibrary.Build(null, [new IconPakSource(BuildModPak(), "Mod")], embeddedItemAtlases: false);

        var status = lib.Find("Mod_Status")!;
        var tile = lib.GetTile(status, out var w, out var h);
        Assert.Equal((64, 64), (w, h));
        Assert.Equal(255, tile![2]); // blue half of the atlas

        var thumb = lib.GetThumbnail(lib.Find("Mod_Spell")!, out w, out h);
        Assert.Equal((64, 64), (w, h));
        Assert.Equal(255, thumb![0]); // red half

        var large = lib.GetLarge(lib.Find("Mod_Spell")!, out w, out h);
        Assert.Equal((8, 8), (w, h));
        Assert.Equal(255, large![1]);

        // No tile: the grid shows the picture, scaled down.
        Assert.NotNull(lib.GetThumbnail(lib.Find("Mod_Ring")!, out _, out _));
    }

    [Fact]
    public void EmbeddedItemAtlases_StandInWithoutTheGame()
    {
        var lib = IconLibrary.Build(null, []);
        Assert.Contains(lib.All, e => e.Kind == IconKind.Item && e.IsVanilla && e.HasTile);
        Assert.Null(lib.GameDataDir);
    }
}
