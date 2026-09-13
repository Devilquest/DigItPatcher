using System.Buffers.Binary;

namespace DigItPatcher.Tests;

/// <summary>Builds an executable carrying nothing but an NE segment table, so a test can place a site where a given build puts it.</summary>
internal static class FakeMainExe
{
    /// <summary>Where the full release starts the two segments this tool reads from.</summary>
    public const int FullSeg3 = 0x0AC00, FullDGroup = 0x20100;

    /// <summary>Where the Manaccom edition starts them, 512 bytes on.</summary>
    public const int ManaccomSeg3 = 0x0AE00, ManaccomDGroup = 0x20300;

    private const int HeaderAt = 0x80;
    private const int TableFromHeader = 0x40;
    private const int SegmentCount = 7;
    private const int DGroupNumber = 6;
    private const int AlignmentShift = 8;
    private const int RecordSize = 8;

    /// <summary>An executable of <paramref name="length"/> bytes whose segment table puts seg3 and DGROUP where asked.</summary>
    public static byte[] WithSegments(int seg3, int dgroup, int length)
    {
        var exe = new byte[length];

        BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(0x3C), HeaderAt);
        exe[HeaderAt] = (byte)'N';
        exe[HeaderAt + 1] = (byte)'E';
        Word(exe, HeaderAt + 0x0E, DGroupNumber);
        Word(exe, HeaderAt + 0x1C, SegmentCount);
        Word(exe, HeaderAt + 0x22, TableFromHeader);
        Word(exe, HeaderAt + 0x32, AlignmentShift);

        int table = HeaderAt + TableFromHeader;
        Word(exe, table + (2 * RecordSize), seg3 >> AlignmentShift);
        Word(exe, table + ((DGroupNumber - 1) * RecordSize), dgroup >> AlignmentShift);

        return exe;
    }

    /// <summary>The same, shaped like the full release.</summary>
    public static byte[] FullRelease(int length) => WithSegments(FullSeg3, FullDGroup, length);

    private static void Word(byte[] bytes, int offset, int value)
        => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), (ushort)value);
}
