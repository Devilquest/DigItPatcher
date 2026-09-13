namespace DigItPatcher.Core.Formats;

/// <summary>The engine's proportional bitmap font: a 26x5 grid of 12x12 glyph cells embedded in <c>MAIN.EXE</c>.</summary>
public sealed class GameFont
{
    /// <summary>Glyph cell size in pixels (both dimensions).</summary>
    public const int CellSize = 12;

    /// <summary>Glyph grid width, in cells.</summary>
    public const int Columns = 26;

    private const int Spacing = 1;
    private const int BlankWidth = 4;
    private const byte FallbackCode = 0x7C;

    /// <summary>Address within seg3 and length of the glyph page's frame container.</summary>
    public const int GlyphBlobSite = 0xE1B6, GlyphBlobLength = 3490;

    /// <summary>Address within seg3 and length of the CP437-to-glyph translation table.</summary>
    public const int XlatSite = 0xEF58, XlatLength = 256;

    /// <summary>Default white-to-gray text color ramp mapped to palette indices.</summary>
    public static readonly byte[] TextRamp = [0, 124, 126, 128, 129, 131, 133, 135, 137, 138, 140, 142, 143, 0, 0, 0];

    /// <summary>Base palette indices for alternative text color ramps (styles 1..7).</summary>
    private static readonly byte[] StyleBase = [0, 132, 180, 144, 112, 156, 200, 128];

    /// <summary>Highest color ramp style supported by <see cref="Ramp"/>.</summary>
    public const int MaxRampStyle = 7;

    /// <summary>The 16-entry ink-remap table for a color style: <see cref="TextRamp"/> for style 0, or 12
    /// consecutive palette indices starting at <see cref="StyleBase"/>[style] for 1..7.</summary>
    public static byte[] Ramp(int style)
    {
        if (style == 0) return TextRamp;
        var ramp = new byte[16];
        for (int i = 0; i < 12; i++) ramp[1 + i] = (byte)(StyleBase[style] + i);
        return ramp;
    }

    /// <summary>The table <see cref="Ramp"/> installs, with every ink level taking the color of the level
    /// above it, which is the only way to reach a ramp's first entry.</summary>
    public static byte[] BrightRamp(int style)
    {
        var ramp = Ramp(style);
        var bright = new byte[16];
        for (int i = 1; i <= 12; i++) bright[i] = ramp[Math.Max(1, i - 1)];
        return bright;
    }

    /// <summary>The decoded 320x200 glyph page, palette indices 0..12 (0 = ink-free).</summary>
    public byte[] Page { get; }

    private readonly byte[] _xlat;

    /// <summary>Advance width per glyph code (0..127), measured from the page's own ink.</summary>
    public IReadOnlyList<int> Widths { get; }

    /// <summary>Blit height, the lowest ink row, 0-based, per glyph code (0..127).</summary>
    public IReadOnlyList<int> Heights { get; }

    private GameFont(byte[] page, byte[] xlat, int[] widths, int[] heights)
    {
        Page = page;
        _xlat = xlat;
        Widths = widths;
        Heights = heights;
    }

    /// <summary>Decodes the glyph page and measures its metrics from the raw <c>MAIN.EXE</c> byte ranges.</summary>
    public static GameFont Load(ReadOnlySpan<byte> glyphBlob, ReadOnlySpan<byte> xlatTable)
    {
        var (_, chunks) = FrameContainer.Split(glyphBlob);
        var pages = FrameContainer.DecodePages(chunks);
        return FromPage(pages[0], xlatTable);
    }

    private static GameFont FromPage(byte[] page, ReadOnlySpan<byte> xlatTable)
    {
        var (widths, heights) = Measure(page);
        return new GameFont(page, xlatTable.ToArray(), widths, heights);
    }

    /// <summary>Maps a CP437 byte to its glyph cell index.</summary>
    public int Code(byte b) => b <= 0x7F ? b : (_xlat[b] != 0 ? _xlat[b] : FallbackCode);

    /// <summary>Pixel width of a byte string (spacing between glyphs, none trailing).</summary>
    public int TextWidth(ReadOnlySpan<byte> s)
    {
        if (s.Length == 0) return 0;
        int total = 0;
        foreach (var b in s) total += Widths[Code(b)] + Spacing;
        return total - Spacing;
    }

    /// <summary>Renders CP437 text into a canvas using ink remapping through the given color ramp.</summary>
    public void Draw(byte[] canvas, int canvasWidth, int canvasHeight, int x, int y, ReadOnlySpan<byte> s,
                      IReadOnlyList<byte>? ramp = null)
    {
        ramp ??= TextRamp;
        int pen = x;
        foreach (var b in s)
        {
            int code = Code(b);
            int row = code / Columns, col = code % Columns;
            int w = Widths[code], h = Heights[code];
            for (int gy = 0; gy <= h; gy++)
            {
                int cy = y + gy;
                if (cy < 0 || cy >= canvasHeight) continue;
                // Inclusive column bounds: Measure stores the last ink column index, not a count.
                for (int gx = 0; gx <= w; gx++)
                {
                    int cx = pen + gx;
                    if (cx < 0 || cx >= canvasWidth) continue;
                    byte v = Page[((row * CellSize) + gy) * FrameCodec.Width + (col * CellSize) + gx];
                    if (v != 0) canvas[(cy * canvasWidth) + cx] = ramp[v & 0x0F];
                }
            }

            pen += w + Spacing;
        }
    }

    /// <summary>Blits centered on <paramref name="cx"/> (seg3:0x047E), landing an odd-width string one
    /// pixel left of true center.</summary>
    public void DrawCentered(byte[] canvas, int canvasWidth, int canvasHeight, int cx, int y, ReadOnlySpan<byte> s,
                              IReadOnlyList<byte>? ramp = null) =>
        Draw(canvas, canvasWidth, canvasHeight, cx - (TextWidth(s) >> 1), y, s, ramp);

    /// <summary>Blits right-aligned on <paramref name="right"/>: the pen starts at <c>right -
    /// TextWidth(s)</c>.</summary>
    public void DrawRightAligned(byte[] canvas, int canvasWidth, int canvasHeight, int right, int y, ReadOnlySpan<byte> s,
                                  IReadOnlyList<byte>? ramp = null) =>
        Draw(canvas, canvasWidth, canvasHeight, right - TextWidth(s), y, s, ramp);

    /// <summary>Draws the string at each of its four orthogonal neighbors in <paramref name="outline"/>,
    /// then draws it again on top in <paramref name="ramp"/>.</summary>
    public void DrawOutlined(byte[] canvas, int canvasWidth, int canvasHeight, int x, int y, ReadOnlySpan<byte> s,
                              IReadOnlyList<byte> ramp, byte outline)
    {
        var silhouette = new byte[16];
        for (int i = 1; i < silhouette.Length; i++) silhouette[i] = outline;

        Draw(canvas, canvasWidth, canvasHeight, x - 1, y, s, silhouette);
        Draw(canvas, canvasWidth, canvasHeight, x + 1, y, s, silhouette);
        Draw(canvas, canvasWidth, canvasHeight, x, y - 1, s, silhouette);
        Draw(canvas, canvasWidth, canvasHeight, x, y + 1, s, silhouette);
        Draw(canvas, canvasWidth, canvasHeight, x, y, s, ramp);
    }

    /// <summary>The outlined draw, centered on <paramref name="cx"/> the same way <see cref="DrawCentered"/> is.</summary>
    public void DrawOutlinedCentered(byte[] canvas, int canvasWidth, int canvasHeight, int cx, int y, ReadOnlySpan<byte> s,
                                      IReadOnlyList<byte> ramp, byte outline) =>
        DrawOutlined(canvas, canvasWidth, canvasHeight, cx - (TextWidth(s) >> 1), y, s, ramp, outline);

    /// <summary>Measures advance widths and blit heights directly from glyph page ink pixels.</summary>
    private static (int[] Widths, int[] Heights) Measure(byte[] page)
    {
        var widths = new int[256];
        var heights = new int[256];
        for (int code = 0; code < 128; code++)
        {
            int row = code / Columns, col = code % Columns;
            int maxX = 0, lastY = 0;
            for (int y = 0; y < CellSize; y++)
            {
                for (int x = CellSize - 1; x >= 0; x--)
                {
                    if (page[((row * CellSize) + y) * FrameCodec.Width + (col * CellSize) + x] != 0)
                    {
                        maxX = Math.Max(maxX, x);
                        lastY = y;
                        break;
                    }
                }
            }

            widths[code] = maxX != 0 ? maxX : BlankWidth;
            heights[code] = lastY;
        }

        return (widths, heights);
    }
}
