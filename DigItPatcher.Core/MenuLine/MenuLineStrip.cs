using DigItPatcher.Core.Formats;

namespace DigItPatcher.Core.MenuLine;

/// <summary>Grows the copyright strip's cell to hold the main menu's own line, and centers the text in it.</summary>
internal static class MenuLineStrip
{
    /// <summary>The pristine cell's own height, border included.</summary>
    public const int OriginalHeight = 9;

    /// <summary>One line's own band within the cell: one blank row, five lettering rows, one blank row.</summary>
    public const int Band = OriginalHeight - 2;

    private const int WordGap = 2;

    /// <summary>A cell's border index and its width and height, both border included.</summary>
    public readonly record struct Cell(byte Border, int Width, int Height);

    /// <summary>Reads a cell's border index, width, and height straight off the page.</summary>
    public static Cell ReadCell(byte[] strip)
    {
        var border = strip[0];
        int width = 1;
        while (width < FrameCodec.Width && strip[width] == border) width++;
        int height = 1;
        while (height < FrameCodec.Height && strip[height * FrameCodec.Width] == border) height++;
        return new Cell(border, width, height);
    }

    /// <summary>The rebuilt strip and the interior row count its blit source rect should copy.</summary>
    public readonly record struct Composition(byte[] Strip, int Rows);

    /// <summary>Grows the cell to hold every line in <paramref name="lines"/>, discarding and rebuilding
    /// everything from the pristine cell's own bottom border down, whatever height was found there.</summary>
    /// <param name="strip">A decoded page holding the cell, <see cref="FrameCodec.Width"/> wide.</param>
    /// <param name="font">Every character the lines may use, already inked (<see cref="MenuLineFont.Glyphs"/>).</param>
    /// <param name="background">The cell's own background color.</param>
    /// <param name="lines">The lines to add, one band each, in order.</param>
    public static Composition Compose(byte[] strip, IReadOnlyDictionary<char, byte[][]> font, byte background,
        IReadOnlyList<string> lines)
    {
        var cell = ReadCell(strip);
        if (cell.Height < OriginalHeight || (cell.Height - OriginalHeight) % Band != 0)
            throw new InvalidOperationException(
                $"the strip is {cell.Width}x{cell.Height}, not a height this tool can rebuild from " +
                $"({OriginalHeight}, plus any multiple of {Band})");

        var page = (byte[])strip.Clone();
        int newHeight = OriginalHeight + Band * lines.Count;

        for (int y = OriginalHeight - 1; y < newHeight - 1; y++)
            for (int x = 0; x < cell.Width; x++)
                page[y * FrameCodec.Width + x] = x is 0 || x == cell.Width - 1 ? cell.Border : background;
        for (int x = 0; x < cell.Width; x++)
            page[(newHeight - 1) * FrameCodec.Width + x] = cell.Border;

        for (int i = 0; i < lines.Count; i++)
            Draw(page, lines[i].ToUpperInvariant(), font, background, cell.Width, OriginalHeight + Band * i);

        return new Composition(page, newHeight - 2);
    }

    private static (int Total, List<(byte[][] Glyph, int Pen)> Placed) Measure(
        string text, IReadOnlyDictionary<char, byte[][]> font)
    {
        var missing = text.Where(c => c != ' ' && !font.ContainsKey(c)).Distinct().ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"no glyph for {string.Join(", ", missing.Select(c => $"'{c}'"))} - the alphabet this tool " +
                $"draws is {string.Concat(font.Keys.OrderBy(c => c))}");

        var placed = new List<(byte[][], int)>();
        int pen = 0;
        foreach (var c in text)
        {
            if (c == ' ') { pen += WordGap + 1; continue; }
            var glyph = font[c];
            placed.Add((glyph, pen));
            pen += glyph[0].Length + 1;
        }
        return (placed.Count == 0 ? 0 : pen - 1, placed);
    }

    private static void Draw(byte[] page, string text, IReadOnlyDictionary<char, byte[][]> font, byte background,
        int cellWidth, int top)
    {
        var (total, placed) = Measure(text, font);
        if (total > cellWidth - 2)
            throw new InvalidOperationException($"the line is {total} px wide and the strip is {cellWidth - 2}");

        int left = 1 + (cellWidth - 2 - total) / 2;
        foreach (var (glyph, pen) in placed)
            for (int dy = 0; dy < glyph.Length; dy++)
                for (int dx = 0; dx < glyph[dy].Length; dx++)
                {
                    var value = glyph[dy][dx];
                    if (value != background)
                        page[(top + dy) * FrameCodec.Width + left + pen + dx] = value;
                }
    }
}
