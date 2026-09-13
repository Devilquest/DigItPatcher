namespace DigItPatcher.Core.Formats;

/// <summary>How far a decode got, which is what tells a correctly aligned frame from a displaced one.</summary>
/// <param name="Ok">Whether every opcode stayed inside its input and its output buffer.</param>
/// <param name="Consumed">Bytes of the compressed stream the decode read.</param>
/// <param name="Produced">Bytes of image the decode wrote.</param>
public readonly record struct FrameDecode(bool Ok, int Consumed, int Produced);

/// <summary>The compression the game's image files use, read only far enough to tell a sound frame from a broken one.</summary>
public static class FrameCodec
{
    /// <summary>Frame width in pixels.</summary>
    public const int Width = 320;

    /// <summary>Frame height in pixels.</summary>
    public const int Height = 200;

    /// <summary>Bytes one decoded frame holds.</summary>
    public const int FrameBytes = Width * Height;

    // Runs may overshoot the frame and are trimmed, so the buffer carries room past the image itself.
    private const int Slack = 0x4000;

    /// <summary>Size a decode's output buffer has to have.</summary>
    public const int WorkingSize = FrameBytes + Slack;

    /// <summary>The ceiling a compressed frame's size field can hold.</summary>
    public const int MaxCompressedSize = 0xFFFF;

    /// <summary>Whether a frame decodes as an intact stream: every opcode in range, the whole input read, a full image out.</summary>
    public static bool IsIntact(ReadOnlySpan<byte> frame, byte[] working)
    {
        var decode = Decompress(frame, working);
        return decode.Ok && decode.Consumed == frame.Length && decode.Produced == FrameBytes;
    }

    /// <summary>Decodes one compressed frame into <paramref name="working"/> and reports how far it got.</summary>
    public static FrameDecode Decompress(ReadOnlySpan<byte> src, byte[] working)
    {
        int di = 0, si = 0, n = src.Length, cap = working.Length;

        while (di < FrameBytes && si < n)
        {
            int b = src[si];
            switch (b >> 5) // top 3 bits = opcode
            {
                case 0: // copy N literal bytes
                {
                    int count = (b & 0x1F) + 1;
                    si += 1;
                    if (si + count > n || di + count > cap) return new FrameDecode(false, si, di);
                    src.Slice(si, count).CopyTo(working.AsSpan(di, count));
                    si += count;
                    di += count;
                    break;
                }

                case 1: // skip N output bytes (13-bit count)
                    if (si + 2 > n) return new FrameDecode(false, si, di);
                    di += (((b << 8) | src[si + 1]) & 0x1FFF) + 1;
                    si += 2;
                    if (di > cap) return new FrameDecode(false, si, di);
                    break;

                case 2: // skip N output bytes (5-bit count)
                    di += (b & 0x1F) + 1;
                    si += 1;
                    if (di > cap) return new FrameDecode(false, si, di);
                    break;

                case 3: // write the next u16 value N times, and this count alone is not incremented
                {
                    if (si + 4 > n) return new FrameDecode(false, si, di);
                    int count = ((b << 8) | src[si + 1]) & 0x1FFF;
                    si += 2;
                    if (di + (2 * count) > cap) return new FrameDecode(false, si, di);
                    byte low = src[si], high = src[si + 1];
                    for (int k = 0; k < count; k++)
                    {
                        working[di++] = low;
                        working[di++] = high;
                    }

                    si += 2;
                    break;
                }

                case 4: // fill N bytes with the next byte (13-bit count)
                {
                    if (si + 3 > n) return new FrameDecode(false, si, di);
                    int count = (((b << 8) | src[si + 1]) & 0x1FFF) + 1;
                    si += 2;
                    if (di + count > cap) return new FrameDecode(false, si, di);
                    working.AsSpan(di, count).Fill(src[si]);
                    si += 1;
                    di += count;
                    break;
                }

                case 5: // fill N bytes with the next byte (5-bit count)
                {
                    if (si + 2 > n) return new FrameDecode(false, si, di);
                    int count = (b & 0x1F) + 1;
                    if (di + count > cap) return new FrameDecode(false, si, di);
                    working.AsSpan(di, count).Fill(src[si + 1]);
                    si += 2;
                    di += count;
                    break;
                }

                case 6: // copy N bytes from earlier output, by distance back, which may overlap
                {
                    if (si + 3 > n) return new FrameDecode(false, si, di);
                    int distance = (((b << 8) | src[si + 1]) & 0x1FFF) + 1;
                    si += 2;
                    int count = src[si] + 1;
                    si += 1;
                    int from = di - distance;
                    if (from < 0 || from + count > cap || di + count > cap) return new FrameDecode(false, si, di);
                    for (int k = 0; k < count; k++) working[di++] = working[from++];
                    break;
                }

                default: // copy N bytes from earlier output, by absolute position
                {
                    if (si + 3 > n) return new FrameDecode(false, si, di);
                    int count = (b & 0x1F) + 1;
                    si += 1;
                    int from = src[si] | (src[si + 1] << 8);
                    si += 2;
                    if (from + count > cap || di + count > cap) return new FrameDecode(false, si, di);
                    for (int k = 0; k < count; k++) working[di++] = working[from++];
                    break;
                }
            }
        }

        return new FrameDecode(true, si, di);
    }

    /// <summary>Encodes a full frame using only literal runs and fills, the two opcodes that never refer to anything outside the stream.</summary>
    public static byte[] Compress(ReadOnlySpan<byte> plane)
    {
        if (plane.Length != FrameBytes) throw new ArgumentException($"expected {FrameBytes} bytes, got {plane.Length}", nameof(plane));

        var output = new List<byte>();
        var literal = new List<byte>(32);

        void FlushLiteral()
        {
            for (int i = 0; i < literal.Count; i += 32)
            {
                int count = Math.Min(32, literal.Count - i);
                output.Add((byte)(count - 1)); // op 0
                output.AddRange(literal.GetRange(i, count));
            }

            literal.Clear();
        }

        int p = 0;
        while (p < FrameBytes)
        {
            int run = 1;
            while (p + run < FrameBytes && plane[p + run] == plane[p] && run < 8192) run++;

            if (run < 3) // too short to pay for a fill opcode
            {
                for (int k = 0; k < run; k++) literal.Add(plane[p + k]);
            }
            else
            {
                FlushLiteral();
                if (run <= 32)
                {
                    output.Add((byte)(0xA0 | (run - 1))); // op 5
                    output.Add(plane[p]);
                }
                else
                {
                    output.Add((byte)(0x80 | ((run - 1) >> 8))); // op 4
                    output.Add((byte)((run - 1) & 0xFF));
                    output.Add(plane[p]);
                }
            }

            p += run;
        }

        FlushLiteral();

        if (output.Count > MaxCompressedSize) throw new InvalidOperationException($"frame compresses to {output.Count} bytes, over the u16 limit");
        return [.. output];
    }
}
