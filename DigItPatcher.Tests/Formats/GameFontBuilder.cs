using DigItPatcher.Core.Formats;

namespace DigItPatcher.Tests;

/// <summary>Builds a <see cref="GameFont"/> the tests own outright, with exactly the glyphs a test asks for.</summary>
internal static class GameFontBuilder
{
    /// <summary>A font whose only ink is one pixel per given glyph, at the given width and height inside
    /// that glyph's cell, so <c>Widths</c>/<c>Heights</c> come out exactly as given.</summary>
    internal static GameFont With(params (byte Code, int Width, int Height, byte Ink)[] glyphs)
    {
        var page = new byte[FrameCodec.FrameBytes];
        foreach (var (code, width, height, ink) in glyphs)
        {
            int row = code / GameFont.Columns, col = code % GameFont.Columns;
            int cellX = col * GameFont.CellSize, cellY = row * GameFont.CellSize;
            page[((cellY + height) * FrameCodec.Width) + cellX + width] = ink;
        }

        var blob = FrameContainer.Build(new byte[772], [page]);
        return GameFont.Load(blob, new byte[256]);
    }
}
