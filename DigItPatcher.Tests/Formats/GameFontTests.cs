using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the font's metrics, its plain draw and the outlined draw written for the slab.</summary>
public class GameFontTests
{
    [Fact]
    public void MeasureReadsTheWidthAndHeightOfANonBlankGlyphFromItsOwnInk()
    {
        var font = GameFontBuilder.With((Code: 65, Width: 6, Height: 3, Ink: 4));

        Assert.Equal(6, font.Widths[65]);
        Assert.Equal(3, font.Heights[65]);
    }

    [Fact]
    public void ABlankGlyphGetsTheSpaceWidth()
    {
        var font = GameFontBuilder.With();

        Assert.Equal(4, font.Widths[65]);
    }

    [Fact]
    public void TextWidthSumsAdvanceWidthsPlusSpacingBetweenGlyphsOnly()
    {
        var font = GameFontBuilder.With((Code: (byte)'A', Width: 6, Height: 3, Ink: 4), (Code: (byte)'B', Width: 4, Height: 2, Ink: 5));

        Assert.Equal(6 + 1 + 4, font.TextWidth("AB"u8));
    }

    [Fact]
    public void DrawPlacesTheGlyphsInkAtTheRampColorForItsInkLevel()
    {
        var font = GameFontBuilder.With((Code: (byte)'A', Width: 6, Height: 3, Ink: 4));
        var canvas = new byte[FrameCodec.FrameBytes];
        var ramp = GameFont.Ramp(2);

        font.Draw(canvas, FrameCodec.Width, FrameCodec.Height, 10, 20, "A"u8, ramp);

        Assert.Equal(ramp[4], canvas[((20 + 3) * FrameCodec.Width) + 10 + 6]);
    }

    [Fact]
    public void DrawCenteredStartsThePenAtARightShiftOfHalfTheTextWidth()
    {
        var font = GameFontBuilder.With((Code: (byte)'A', Width: 7, Height: 0, Ink: 4));
        var canvas = new byte[FrameCodec.FrameBytes];

        font.DrawCentered(canvas, FrameCodec.Width, FrameCodec.Height, 100, 20, "A"u8, GameFont.TextRamp);

        int expectedPen = 100 - (font.TextWidth("A"u8) >> 1);
        Assert.Equal(GameFont.TextRamp[4], canvas[(20 * FrameCodec.Width) + expectedPen + 7]);
    }

    [Fact]
    public void DrawOutlinedStampsTheFourNeighborsInOutlineAndTheGlyphOnTopInTheRealRamp()
    {
        var font = GameFontBuilder.With((Code: (byte)'A', Width: 6, Height: 3, Ink: 4));
        var canvas = new byte[FrameCodec.FrameBytes];
        var ramp = GameFont.Ramp(2);
        const byte outline = 95;
        int x = 50, y = 60;

        font.DrawOutlined(canvas, FrameCodec.Width, FrameCodec.Height, x, y, "A"u8, ramp, outline);

        int inkRow = y + 3, inkCol = x + 6;
        Assert.Equal(ramp[4], canvas[(inkRow * FrameCodec.Width) + inkCol]);
        Assert.Equal(outline, canvas[(inkRow * FrameCodec.Width) + inkCol - 1]);
        Assert.Equal(outline, canvas[(inkRow * FrameCodec.Width) + inkCol + 1]);
        Assert.Equal(outline, canvas[((inkRow - 1) * FrameCodec.Width) + inkCol]);
        Assert.Equal(outline, canvas[((inkRow + 1) * FrameCodec.Width) + inkCol]);
    }

    [Fact]
    public void DrawOutlinedCenteredCentersTheSameWayDrawCenteredDoes()
    {
        var font = GameFontBuilder.With((Code: (byte)'A', Width: 7, Height: 0, Ink: 4));
        var outlined = new byte[FrameCodec.FrameBytes];
        var plain = new byte[FrameCodec.FrameBytes];
        var ramp = GameFont.Ramp(1);

        font.DrawOutlinedCentered(outlined, FrameCodec.Width, FrameCodec.Height, 100, 20, "A"u8, ramp, 95);
        font.DrawCentered(plain, FrameCodec.Width, FrameCodec.Height, 100, 20, "A"u8, ramp);

        int expectedPen = 100 - (font.TextWidth("A"u8) >> 1);
        Assert.Equal(plain[(20 * FrameCodec.Width) + expectedPen + 7], outlined[(20 * FrameCodec.Width) + expectedPen + 7]);
    }

    [InlineData('I', 2)] [InlineData('i', 2)] [InlineData('l', 2)] [InlineData('!', 2)] [InlineData('.', 2)] [InlineData('\'', 2)]
    [InlineData('(', 3)] [InlineData(')', 3)] [InlineData(',', 3)]
    [InlineData('"', 5)] [InlineData('t', 5)]
    [InlineData('K', 7)] [InlineData('X', 7)] [InlineData('k', 7)] [InlineData('x', 7)] [InlineData('&', 7)] [InlineData('#', 7)]
    [InlineData('M', 10)] [InlineData('W', 10)] [InlineData('m', 10)] [InlineData('w', 10)]
    [RequiresGameTheory(GameNeeds.SupportedBuild)]
    public void MeasuredWidthsMatchTheDocumentedSamples(char ch, int expectedWidth)
    {
        var font = LoadFromInstall();
        Assert.Equal(expectedWidth, font.Widths[(byte)ch]);
    }

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void AAndTheTallLettersMeasureTheDocumentedBlitHeights()
    {
        var font = LoadFromInstall();
        Assert.Equal(9, font.Heights[(byte)'A']);
        Assert.Equal(11, font.Heights[(byte)'g']);
    }

    [InlineData("A", 6)]
    [InlineData("MAP", 24)]
    [InlineData(">402", 26)]
    [RequiresGameTheory(GameNeeds.SupportedBuild)]
    public void TextWidthMatchesTheDocumentedResults(string s, int expected)
    {
        var font = LoadFromInstall();
        Assert.Equal(expected, font.TextWidth(System.Text.Encoding.ASCII.GetBytes(s)));
    }

    private static GameFont LoadFromInstall()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));
        var exe = install.ReadAll("MAIN.EXE");
        Assert.True(MainExeLayout.TryRead(exe, out var layout));
        return GameFont.Load(exe.AsSpan(layout.Seg3(GameFont.GlyphBlobSite), GameFont.GlyphBlobLength),
                              exe.AsSpan(layout.Seg3(GameFont.XlatSite), GameFont.XlatLength));
    }
}
