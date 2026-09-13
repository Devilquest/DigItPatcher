using DigItPatcher.Core.Formats;

namespace DigItPatcher.Core.Slab;

/// <summary>The slab's own words: the heading, the credit line and the author, already substituted.</summary>
internal readonly record struct SlabWording(string Heading, string Credit, string Author);

/// <summary>Paints the patch-notes layout onto a blank slab strip.</summary>
internal static class SlabNotes
{
    // Camera step: one slab occupies this many rows of the strip, and its own layout is measured
    // from the top of that window rather than from the strip, so the same rows serve any slab.
    internal const int Step = 150;

    internal const int TitleRow = 54, ListRow = 72, Leading = 14, CreditRow = 118, NameRow = 132;
    private const int SlabLeft = 38, SlabRight = 280;

    private const byte White = 0, Red = 2, Green = 3, Amber = 5;

    // The near-black the outline uses. The engine outlines in index 0, which is the slab plane's
    // transparent key, so a baked copy has to name a real color instead.
    private const byte Outline = 95;

    /// <summary>Paints <paramref name="wording"/> and <paramref name="lines"/> for one slab onto
    /// <paramref name="strip"/>, in place.</summary>
    public static void Paint(byte[] strip, GameFont font, int slab, SlabWording wording, IReadOnlyList<string> lines)
    {
        int top = Step * (slab - 1);
        int height = strip.Length / FrameCodec.Width;
        int center = FrameCodec.Width / 2;

        var title = CheckFits(font, wording.Heading);
        var credit = CheckFits(font, wording.Credit);
        var author = CheckFits(font, wording.Author);
        var lineBytes = lines.Select(line => CheckFits(font, line)).ToList();

        font.DrawOutlinedCentered(strip, FrameCodec.Width, height, center, top + TitleRow, title, GameFont.BrightRamp(Amber), Outline);

        // The list is left-aligned as a block, and the block is centered: centering each line
        // separately would stop it reading as a list.
        int left = center - (lineBytes.Count == 0 ? 0 : lineBytes.Max(l => font.TextWidth(l)) >> 1);
        for (int i = 0; i < lineBytes.Count; i++)
            font.DrawOutlined(strip, FrameCodec.Width, height, left, top + ListRow + (i * Leading), lineBytes[i], GameFont.Ramp(White), Outline);

        font.DrawOutlinedCentered(strip, FrameCodec.Width, height, center, top + CreditRow, credit, GameFont.Ramp(Red), Outline);
        font.DrawOutlinedCentered(strip, FrameCodec.Width, height, center, top + NameRow, author, GameFont.BrightRamp(Green), Outline);
    }

    private static byte[] CheckFits(GameFont font, string line)
    {
        var bytes = AsciiBytes(line);
        int width = font.TextWidth(bytes);
        if (width > SlabRight - SlabLeft)
            throw new InvalidOperationException($"\"{line}\" is {width}px wide, exceeding the {SlabRight - SlabLeft}px the stone allows");

        return bytes;
    }

    private static byte[] AsciiBytes(string s)
    {
        var bytes = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] > 0x7F) throw new ArgumentException($"\"{s}\" is not plain ASCII text", nameof(s));
            bytes[i] = (byte)s[i];
        }

        return bytes;
    }
}
