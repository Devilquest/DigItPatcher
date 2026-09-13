using DigItPatcher.Core.Formats;
using DigItPatcher.Core.MenuLine;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the main menu line's alphabet: every glyph is authored, and only the strip's
/// own colors are ever read from the game.</summary>
public class MenuLineFontTests
{
    private const byte Background = 0, Main = 127, Soft = 133;

    // A 9-row cell like the strip's own, background everywhere inside, with Main outnumbering Soft so
    // ReadInk has an unambiguous frequency order to find.
    private static byte[] BuildCell(int width, int height)
    {
        var page = new byte[FrameCodec.Width * FrameCodec.Height];
        for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
                page[y * FrameCodec.Width + x] = Background;

        page[2 * FrameCodec.Width + 2] = Main;
        page[2 * FrameCodec.Width + 3] = Main;
        page[3 * FrameCodec.Width + 2] = Main;
        page[4 * FrameCodec.Width + 2] = Soft;

        return page;
    }

    [Fact]
    public void ReadInkFindsTheBackgroundAndBothInksByHowOftenTheyAppear()
    {
        var ink = MenuLineFont.ReadInk(BuildCell(246, 9), 246, 9);

        Assert.Equal(Background, ink.Background);
        Assert.Equal(Main, ink.Main);
        Assert.Equal(Soft, ink.Soft);
    }

    [Fact]
    public void EveryGlyphNeededByTheMenuLineAlphabetIsAuthored()
    {
        var glyphs = MenuLineFont.Glyphs(new MenuLineFont.Ink(Background, Main, Soft));

        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.!+-";
        Assert.All(alphabet, c => Assert.True(glyphs.ContainsKey(c), $"missing glyph for '{c}'"));
        Assert.Equal(alphabet.Length, glyphs.Count);
    }

    [Fact]
    public void EveryGlyphIsFiveRowsTallAndUsesOnlyTheThreeGivenColors()
    {
        var glyphs = MenuLineFont.Glyphs(new MenuLineFont.Ink(Background, Main, Soft));

        Assert.All(glyphs, entry =>
        {
            Assert.Equal(MenuLineFont.GlyphHeight, entry.Value.Length);
            Assert.All(entry.Value, row => Assert.All(row, v => Assert.Contains(v, new[] { Background, Main, Soft })));
        });
    }

    [Fact]
    public void TheDigitZeroCarriesTheSoftInkSoItCannotBeReadAsTheLetterO()
    {
        var glyphs = MenuLineFont.Glyphs(new MenuLineFont.Ink(Background, Main, Soft));

        Assert.Contains(Soft, glyphs['0'].SelectMany(row => row));
        Assert.NotEqual(glyphs['O'], glyphs['0']);
    }

    [Fact]
    public void ReinkingTheSameShapesWithDifferentColorsChangesOnlyTheColors()
    {
        var first = MenuLineFont.Glyphs(new MenuLineFont.Ink(0, 127, 133));
        var second = MenuLineFont.Glyphs(new MenuLineFont.Ink(200, 201, 202));

        bool SameShape(byte[][] a, byte[][] b) =>
            a.Length == b.Length && a.Zip(b).All(pair => pair.First.Length == pair.Second.Length);

        // The grid is identical regardless of ink, but a copy inked differently paints different colors.
        Assert.All(first.Keys, c => Assert.True(SameShape(first[c], second[c])));
        Assert.All(second.Values, rows => Assert.All(rows, row => Assert.All(row, v => Assert.True(v is 200 or 201 or 202))));
        Assert.NotEqual(first['A'], second['A']);
    }
}
