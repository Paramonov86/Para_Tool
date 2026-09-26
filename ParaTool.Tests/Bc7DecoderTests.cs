using ParaTool.Core.Textures;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// The game's tooltip icons are BC7. Vectors are blocks from the game's own icons with the pixels
/// Pillow decodes for them; ParaTool matched Pillow on all 486 BC7 icons it was checked against.
/// </summary>
public class Bc7DecoderTests
{
    [Theory]
    [InlineData(1, "56fcefff2d2de30c05819500a59ef9b3", "f5bb37fff5bb37fff5bb37fff3b732fff9c940fffccb49fffac745fff6bf3bfffbd35bfffbd35bfffdcf4efffac745fffde181fffde181fffbd766fffccb49ff")]
    [InlineData(3, "28fcfbffbf7aac3e9f422cd31bbcbe9e", "ffd54fffffcf4dffffd559ffffd559fffccc47ffffcf4dffffd355ffffd355fffbc743ffffcf4dffffd355ffffd355fffbc743ffffcf4dffffd559ffffd355ff")]
    [InlineData(4, "90ff93020000acaaaaaa76cb922459b6", "ff240000ff280000ff270000ff270000ff260000ff270000ff260000ff260000ff260000ff260000ff260000ff260000ff270000ff260000ff270000ff270000")]
    [InlineData(5, "20f033a5423808440c365fff0155aaff", "db290a02db290a02e1280802e1280802cf2a0e07d5290c07db290a07d5290c07cf2a0e0ccf2a0e0cd5290c0cd5290c0ccf2a0e11cf2a0e11cf2a0e11cf2a0e11")]
    [InlineData(6, "c07f7e4201080000b07e5055b3799965", "fe260000f5270300f3280400f8270200fe260000fa270100fa270100fa270100fc260100f5270300f7270200f8270200f7270200f7270200fa270100f9270200")]
    [InlineData(7, "808be64f97834c18450030c096286b39", "dc311104e2371704d72c0c04dc311104d72c0c04dc311104dc311104e2371704dc311104dc311104e73c1c04b8381b0bd72c0c04d7341404983d211179412818")]
    public void Block_DecodesLikeReference(int mode, string blockHex, string pixelsHex)
    {
        var block = Convert.FromHexString(blockHex);
        Assert.Equal(mode, System.Numerics.BitOperations.TrailingZeroCount(block[0]));
        var pixels = new byte[64];
        Bc7Decoder.DecodeBlock(block, pixels);
        Assert.Equal(pixelsHex, Convert.ToHexString(pixels).ToLowerInvariant());
    }

    [Fact]
    public void Anchors_BelongToTheirSubsets()
    {
        for (int p = 0; p < 64; p++)
        {
            Assert.Equal(0, Bc7Decoder.Partitions2[p][0]);
            Assert.Equal(1, Bc7Decoder.Partitions2[p][Bc7Decoder.Anchor2[p]]);
            Assert.Equal(0, Bc7Decoder.Partitions3[p][0]);
            Assert.Equal(1, Bc7Decoder.Partitions3[p][Bc7Decoder.Anchor3a[p]]);
            Assert.Equal(2, Bc7Decoder.Partitions3[p][Bc7Decoder.Anchor3b[p]]);
        }
    }

    [Fact]
    public void Dds_Bc7_DecodesThroughDdsReader()
    {
        // 4×4 DX10 BC7 file around the mode 6 block above.
        var header = new byte[148];
        "DDS "u8.CopyTo(header);
        BitConverter.GetBytes(124).CopyTo(header, 4);
        BitConverter.GetBytes(4).CopyTo(header, 12);   // height
        BitConverter.GetBytes(4).CopyTo(header, 16);   // width
        BitConverter.GetBytes(32).CopyTo(header, 76);  // pixel format size
        BitConverter.GetBytes(4).CopyTo(header, 80);   // DDPF_FOURCC
        "DX10"u8.CopyTo(header.AsSpan(84));
        BitConverter.GetBytes(98).CopyTo(header, 128); // DXGI_FORMAT_BC7_UNORM
        var dds = header.Concat(Convert.FromHexString("c07f7e4201080000b07e5055b3799965")).ToArray();

        var (w, h, rgba) = DdsReader.Decode(dds);
        Assert.Equal((4, 4), (w, h));
        Assert.Equal("fe260000", Convert.ToHexString(rgba, 0, 4).ToLowerInvariant());
    }
}
