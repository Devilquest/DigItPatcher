using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Slab;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the patch-notes layout painted onto a blank slab strip.</summary>
public class SlabNotesTests
{
    private const int Width = FrameCodec.Width;
    private const int Center = Width / 2;

    // Distinct codes, widths, heights, and ink levels per line, so each one's pixel is unambiguous.
    private static GameFont Font() => GameFontBuilder.With(
        (Code: (byte)'H', Width: 4, Height: 2, Ink: 6),  // heading
        (Code: (byte)'L', Width: 3, Height: 1, Ink: 7),  // shorter list line
        (Code: (byte)'M', Width: 9, Height: 5, Ink: 8),  // longer list line
        (Code: (byte)'C', Width: 5, Height: 3, Ink: 9),  // credit
        (Code: (byte)'U', Width: 7, Height: 4, Ink: 10)); // author

    private static readonly SlabWording Wording = new("H", "C", "U");
    private static readonly string[] Lines = ["L", "M"];

    [Fact]
    public void TheHeadingIsCenteredAndUsesTheBrightAmberRamp()
    {
        var font = Font();
        var strip = new byte[Width * 200];

        SlabNotes.Paint(strip, font, 1, Wording, Lines);

        int pen = Center - (font.TextWidth("H"u8) >> 1);
        Assert.Equal(GameFont.BrightRamp(5)[6], strip[((SlabNotes.TitleRow + 2) * Width) + pen + 4]);
    }

    [Fact]
    public void TheListIsLeftAlignedAsABlockCenteredOnItsWidestLine()
    {
        var font = Font();
        var strip = new byte[Width * 200];

        SlabNotes.Paint(strip, font, 1, Wording, Lines);

        int left = Center - (font.TextWidth("M"u8) >> 1); // the wider of the two lines decides the block
        Assert.Equal(GameFont.Ramp(0)[7], strip[((SlabNotes.ListRow + 1) * Width) + left + 3]);
        Assert.Equal(GameFont.Ramp(0)[8], strip[((SlabNotes.ListRow + SlabNotes.Leading + 5) * Width) + left + 9]);
    }

    [Fact]
    public void TheCreditLineUsesPlainRed()
    {
        var font = Font();
        var strip = new byte[Width * 200];

        SlabNotes.Paint(strip, font, 1, Wording, Lines);

        int pen = Center - (font.TextWidth("C"u8) >> 1);
        Assert.Equal(GameFont.Ramp(2)[9], strip[((SlabNotes.CreditRow + 3) * Width) + pen + 5]);
    }

    [Fact]
    public void TheAuthorLineUsesTheBrightGreenRamp()
    {
        var font = Font();
        var strip = new byte[Width * 200];

        SlabNotes.Paint(strip, font, 1, Wording, Lines);

        int pen = Center - (font.TextWidth("U"u8) >> 1);
        Assert.Equal(GameFont.BrightRamp(3)[10], strip[((SlabNotes.NameRow + 4) * Width) + pen + 7]);
    }

    [Fact]
    public void EveryLineIsOutlined()
    {
        var font = Font();
        var strip = new byte[Width * 200];

        SlabNotes.Paint(strip, font, 1, Wording, Lines);

        int pen = Center - (font.TextWidth("H"u8) >> 1);
        int inkRow = SlabNotes.TitleRow + 2, inkCol = pen + 4;
        Assert.Equal(95, strip[(inkRow * Width) + inkCol - 1]);
        Assert.Equal(95, strip[(inkRow * Width) + inkCol + 1]);
    }

    [Fact]
    public void TheRowsShiftByTheCameraStepPerSlab()
    {
        var font = Font();
        var strip = new byte[Width * 400];

        SlabNotes.Paint(strip, font, 3, Wording, Lines);

        int top = SlabNotes.Step * (3 - 1);
        int pen = Center - (font.TextWidth("H"u8) >> 1);
        Assert.Equal(GameFont.BrightRamp(5)[6], strip[((top + SlabNotes.TitleRow + 2) * Width) + pen + 4]);
    }

    [Fact]
    public void ALineWiderThanTheStoneFailsWithAName()
    {
        var font = GameFontBuilder.With((Code: (byte)'W', Width: 11, Height: 5, Ink: 1));
        var strip = new byte[Width * 200];
        var tooWide = new string('W', 40);

        var ex = Assert.Throws<InvalidOperationException>(() => SlabNotes.Paint(strip, font, 1, Wording, [tooWide]));
        Assert.Contains("exceeding", ex.Message);
    }

    [Fact]
    public void NonAsciiWordingIsRejected()
    {
        var font = Font();
        var strip = new byte[Width * 200];

        Assert.Throws<ArgumentException>(() => SlabNotes.Paint(strip, font, 1, Wording with { Heading = "café" }, Lines));
    }
}
