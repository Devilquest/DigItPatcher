using DigItPatcher.Core.Formats;

namespace DigItPatcher.Core.MenuLine;

/// <summary>Every glyph this tool can paint under the copyright strip: 40 characters, authored in the
/// strip's own three-by-five, two-ink style rather than cut out of its picture.</summary>
internal static class MenuLineFont
{
    /// <summary>Glyph width in pixels, before the one blank column every glyph carries after it.</summary>
    public const int GlyphWidth = 3;

    /// <summary>Glyph height in pixels: the strip's five lettering rows.</summary>
    public const int GlyphHeight = 5;

    // `#` is the strip's own main ink, `o` its softer one and `.` is background, all substituted from
    // ReadInk's result below.
    private static readonly Dictionary<char, string[]> Shapes = new()
    {
        ['A'] = ["o#o", "#.#", "###", "#.#", "#.#"],
        ['B'] = ["##o", "#.#", "##o", "#.#", "##o"],
        ['C'] = ["o#o", "#..", "#..", "#..", "o#o"],
        ['D'] = ["##o", "#.#", "#.#", "#.#", "##o"],
        ['E'] = ["###", "#..", "##o", "#..", "###"],
        ['F'] = ["###", "#..", "##o", "#..", "#.."],
        ['G'] = ["o#o", "#..", "#.o", "#.#", "o##"],
        ['H'] = ["#.#", "#.#", "###", "#.#", "#.#"],
        ['I'] = ["o#o", ".#.", ".#.", ".#.", "o#o"],
        ['J'] = ["..#", "..#", "..#", "#.#", "o#o"],
        ['K'] = ["#.#", "#o.", "##.", "#o.", "#.#"],
        ['L'] = ["#..", "#..", "#..", "#..", "###"],
        ['M'] = ["#.#", "###", "#o#", "#.#", "#.#"],
        ['N'] = ["##o", "#.#", "#.#", "#.#", "#.#"],
        ['O'] = ["o#o", "#.#", "#.#", "#.#", "o#o"],
        ['P'] = ["##o", "#.#", "##o", "#..", "#.."],
        ['Q'] = ["o#o", "#.#", "#.#", "#o#", "o##"],
        ['R'] = ["##o", "#.#", "##o", "#.#", "#.#"],
        ['S'] = ["o##", "#..", "o#o", "..#", "##o"],
        ['T'] = ["###", ".#.", ".#.", ".#.", ".#."],
        ['U'] = ["#.#", "#.#", "#.#", "#.#", "o#o"],
        ['V'] = ["#.#", "#.#", "#.#", "o#o", ".#."],
        ['W'] = ["#.#", "#.#", "#o#", "###", "#.#"],
        ['X'] = ["#.#", "#.#", "o#o", "#.#", "#.#"],
        ['Y'] = ["#.#", "#.#", "o#o", ".#.", ".#."],
        ['Z'] = ["###", "..#", "o#o", "#..", "###"],
        ['0'] = ["o#o", "#.#", "#o#", "#.#", "o#o"], // center dot, so it cannot be read as `O`
        ['1'] = [".#.", "o#.", ".#.", ".#.", "o#o"],
        ['2'] = ["o#o", "..#", "o#o", "#..", "###"],
        ['3'] = ["##o", "..#", "o#o", "..#", "##o"],
        ['4'] = ["#.#", "#.#", "###", "..#", "..#"],
        ['5'] = ["###", "#..", "##o", "..#", "##o"],
        ['6'] = ["o#.", "#..", "##o", "#.#", "o#o"],
        ['7'] = ["###", "..#", ".#o", ".#.", ".#."],
        ['8'] = ["o#o", "#.#", "###", "#.#", "o#o"],
        ['9'] = ["o#o", "#.#", "o##", "..#", ".#o"],
        ['.'] = [".", ".", ".", ".", "#"],
        ['!'] = ["#", "#", "#", ".", "#"],
        ['+'] = ["...", ".#.", "###", ".#.", "..."],
        ['-'] = ["...", "...", "###", "...", "..."],
    };

    /// <summary>The strip's own colors: its background and its two ink indices, most frequent first.</summary>
    public readonly record struct Ink(byte Background, byte Main, byte Soft);

    /// <summary>Reads <see cref="Ink"/> out of a cell's interior, excluding its one-pixel border.</summary>
    /// <param name="page">A decoded page, <see cref="FrameCodec.Width"/> wide.</param>
    /// <param name="width">The cell's own width, border included.</param>
    /// <param name="height">The cell's own height, border included.</param>
    public static Ink ReadInk(byte[] page, int width, int height)
    {
        var counts = new Dictionary<byte, int>();
        for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                var value = page[y * FrameCodec.Width + x];
                counts[value] = counts.GetValueOrDefault(value) + 1;
            }

        var byCount = counts.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        return new Ink(byCount[0], byCount[1], byCount[2]);
    }

    /// <summary>Every authored glyph, inked with <paramref name="ink"/>.</summary>
    public static IReadOnlyDictionary<char, byte[][]> Glyphs(Ink ink)
    {
        var colorOf = new Dictionary<char, byte> { ['#'] = ink.Main, ['o'] = ink.Soft, ['.'] = ink.Background };

        return Shapes.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Select(row => row.Select(c => colorOf[c]).ToArray()).ToArray());
    }
}
