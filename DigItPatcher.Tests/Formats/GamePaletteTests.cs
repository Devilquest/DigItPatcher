using DigItPatcher.Core.Formats;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the 6-bit-to-8-bit widening a palette entry's bytes go through.</summary>
public class GamePaletteTests
{
    [Theory]
    [InlineData((byte)0, (byte)0)]
    [InlineData((byte)63, (byte)255)]
    [InlineData((byte)48, (byte)195)]
    [InlineData((byte)16, (byte)65)]
    [InlineData((byte)32, (byte)130)]
    public void EachChannelWidensByReplicatingItsHighBitsIntoTheLowOnes(byte sixBit, byte eightBit)
    {
        var blob = new byte[768];
        blob[0] = sixBit;

        Assert.Equal(eightBit, GamePalette.Read(blob)[0].R);
    }

    [Fact]
    public void AllTwoHundredFiftySixIndicesReadInOrder()
    {
        var blob = new byte[768];
        for (int i = 0; i < GamePalette.ColorCount; i++)
        {
            blob[i * 3] = 1;
            blob[(i * 3) + 1] = 2;
            blob[(i * 3) + 2] = 3;
        }

        var palette = GamePalette.Read(blob);

        Assert.Equal(GamePalette.ColorCount, palette.Length);
        Assert.All(palette, color => Assert.Equal(((byte)4, (byte)8, (byte)12), color));
    }

    [Fact]
    public void AWrongSizedBlobIsRefused()
        => Assert.Throws<ArgumentException>(() => GamePalette.Read(new byte[767]));
}
