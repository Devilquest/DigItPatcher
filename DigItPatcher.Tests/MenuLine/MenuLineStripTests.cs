using DigItPatcher.Core.Formats;
using DigItPatcher.Core.MenuLine;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the copyright strip's own growth: the cell reader, the composition, and that
/// re-patching an already-grown strip comes out the same as patching a pristine one.</summary>
public class MenuLineStripTests
{
    private const byte Border = 112, Background = 0, Main = 127, Soft = 133;

    private static readonly IReadOnlyDictionary<char, byte[][]> Font =
        MenuLineFont.Glyphs(new MenuLineFont.Ink(Background, Main, Soft));

    // A pristine 246x9 cell: border all around, background inside, nothing drawn (the tests draw their own
    // lines, so what the pristine caption itself says does not matter here).
    private static byte[] BuildPristineStrip(int width = 246, int height = MenuLineStrip.OriginalHeight)
    {
        var page = new byte[FrameCodec.Width * FrameCodec.Height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                page[y * FrameCodec.Width + x] =
                    (y == 0 || y == height - 1 || x == 0 || x == width - 1) ? Border : Background;
        return page;
    }

    [Fact]
    public void ReadCellFindsTheBorderAndTheCellsOwnSize()
    {
        var cell = MenuLineStrip.ReadCell(BuildPristineStrip());

        Assert.Equal(Border, cell.Border);
        Assert.Equal(246, cell.Width);
        Assert.Equal(MenuLineStrip.OriginalHeight, cell.Height);
    }

    [Fact]
    public void ComposingOneLineGrowsTheCellByOneBand()
    {
        var result = MenuLineStrip.Compose(BuildPristineStrip(), Font, Background, ["BUG FIXES 1.0"]);

        Assert.Equal(MenuLineStrip.OriginalHeight + MenuLineStrip.Band - 2, result.Rows);
        var cell = MenuLineStrip.ReadCell(result.Strip);
        Assert.Equal(MenuLineStrip.OriginalHeight + MenuLineStrip.Band, cell.Height);
    }

    [Fact]
    public void ComposingDoesNotMutateTheStripPassedIn()
    {
        var pristine = BuildPristineStrip();
        var untouched = (byte[])pristine.Clone();

        MenuLineStrip.Compose(pristine, Font, Background, ["X"]);

        Assert.Equal(untouched, pristine);
    }

    [Fact]
    public void TheNewBottomBorderClosesTheCellOnAllFourSides()
    {
        var result = MenuLineStrip.Compose(BuildPristineStrip(), Font, Background, ["X"]);
        int width = MenuLineStrip.ReadCell(BuildPristineStrip()).Width;
        int bottom = MenuLineStrip.OriginalHeight + MenuLineStrip.Band - 1;

        for (int x = 0; x < width; x++)
            Assert.Equal(Border, result.Strip[bottom * FrameCodec.Width + x]);
        for (int y = MenuLineStrip.OriginalHeight - 1; y <= bottom; y++)
        {
            Assert.Equal(Border, result.Strip[y * FrameCodec.Width]);
            Assert.Equal(Border, result.Strip[y * FrameCodec.Width + width - 1]);
        }
    }

    [Fact]
    public void ComposingTwoLinesPlacesTheSecondOneBandBelowTheFirst()
    {
        var withOne = MenuLineStrip.Compose(BuildPristineStrip(), Font, Background, ["A"]);
        var withTwo = MenuLineStrip.Compose(BuildPristineStrip(), Font, Background, ["A", "A"]);

        // The first line's own band is drawn identically whether or not a second line follows it.
        for (int y = MenuLineStrip.OriginalHeight; y < MenuLineStrip.OriginalHeight + MenuLineStrip.Band - 2; y++)
            for (int x = 0; x < 246; x++)
                Assert.Equal(withOne.Strip[y * FrameCodec.Width + x], withTwo.Strip[y * FrameCodec.Width + x]);

        Assert.Equal(MenuLineStrip.OriginalHeight + 2 * MenuLineStrip.Band - 2, withTwo.Rows);
    }

    [Fact]
    public void ComposingOverAnAlreadyGrownStripWithTheSameLineIsIdempotent()
    {
        var first = MenuLineStrip.Compose(BuildPristineStrip(), Font, Background, ["BUG FIXES 1.0"]);
        var second = MenuLineStrip.Compose(first.Strip, Font, Background, ["BUG FIXES 1.0"]);

        Assert.Equal(first.Rows, second.Rows);
        Assert.Equal(first.Strip, second.Strip);
    }

    [Fact]
    public void TheRowCountComesFromTheRequestedLinesNotFromWhateverHeightWasAlreadyThere()
    {
        var grown = MenuLineStrip.Compose(BuildPristineStrip(), Font, Background, ["ONE", "TWO"]);
        var reduced = MenuLineStrip.Compose(grown.Strip, Font, Background, ["ONE"]);

        Assert.Equal(MenuLineStrip.OriginalHeight + MenuLineStrip.Band - 2, reduced.Rows);
    }

    [Fact]
    public void ALineWiderThanTheCellIsRefused()
    {
        var line = new string('A', 246);

        var ex = Assert.Throws<InvalidOperationException>(
            () => MenuLineStrip.Compose(BuildPristineStrip(), Font, Background, [line]));
        Assert.Contains("px wide", ex.Message);
    }

    [Fact]
    public void ACharacterWithNoGlyphIsRefusedByName()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => MenuLineStrip.Compose(BuildPristineStrip(), Font, Background, ["100% BUGGY"]));
        Assert.Contains("'%'", ex.Message);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(15)]
    public void AStripHeightThatIsNotTheOriginalPlusAWholeBandIsRefused(int height)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => MenuLineStrip.Compose(BuildPristineStrip(height: height), Font, Background, ["X"]));
        Assert.Contains(height.ToString(), ex.Message);
    }
}
