using System.Buffers.Binary;

namespace DigItPatcher.Core.Formats;

/// <summary>Where <c>MAIN.EXE</c>'s segments begin in the file, read from its own NE segment table.</summary>
internal sealed class MainExeLayout
{
    // Field positions: the first is inside the MZ stub, the rest are relative to the NE header it points at.
    private const int NeHeaderPointer = 0x3C;      // u32 file offset of the NE header
    private const int AutoDataSegmentField = 0x0E; // the segment number DGROUP is
    private const int SegmentCountField = 0x1C;
    private const int SegmentTableField = 0x22;    // itself relative to the NE header
    private const int AlignmentShiftField = 0x32;
    private const int RecordSize = 8;              // u16 sector; u16 length; u16 flags; u16 minalloc

    // NE format specifies a default alignment shift of 9 (512 bytes) when the field is 0.
    private const int DefaultAlignmentShift = 9;

    // Beyond this a shift is not a real alignment, and the multiply below would overflow rather than fail.
    private const int MaxAlignmentShift = 16;

    private const int Seg3Number = 3;

    /// <summary>Reads <paramref name="length"/> bytes at an offset, or returns null when they are not there.</summary>
    private delegate byte[]? ReadAt(long offset, int length);

    private readonly int[] _bases;
    private readonly int _dgroupNumber;

    private MainExeLayout(int[] bases, int dgroupNumber)
    {
        _bases = bases;
        _dgroupNumber = dgroupNumber;
    }

    /// <summary>Reads the segment table of an executable held in memory.</summary>
    public static bool TryRead(ReadOnlySpan<byte> mainExe, out MainExeLayout layout)
    {
        // The span cannot be captured, so it is copied once rather than sliced lazily.
        var bytes = mainExe.ToArray();
        return TryRead(
            (offset, length) => offset >= 0 && length >= 0 && offset <= bytes.Length - length
                ? bytes.AsSpan((int)offset, length).ToArray()
                : null,
            out layout);
    }

    /// <summary>Reads the segment table of an executable still on disk, without loading the whole file.</summary>
    public static bool TryRead(Stream stream, out MainExeLayout layout)
        => TryRead(
            (offset, length) =>
            {
                if (offset < 0 || length < 0 || offset > stream.Length - length) return null;
                var buffer = new byte[length];
                stream.Seek(offset, SeekOrigin.Begin);
                stream.ReadExactly(buffer, 0, length);
                return buffer;
            },
            out layout);

    /// <summary>The file offset of a <c>seg3:offset</c> address.</summary>
    public int Seg3(int offset) => _bases[Seg3Number - 1] + offset;

    /// <summary>The file offset of a <c>DS:offset</c> address.</summary>
    public int DGroup(int offset) => _bases[_dgroupNumber - 1] + offset;

    private static bool TryRead(ReadAt read, out MainExeLayout layout)
    {
        layout = null!;

        if (read(NeHeaderPointer, 4) is not { } pointer) return false;
        uint headerAt = BinaryPrimitives.ReadUInt32LittleEndian(pointer);
        if (headerAt > int.MaxValue) return false;

        int header = (int)headerAt;
        if (read(header, 2) is not [(byte)'N', (byte)'E']) return false;

        if (!TryReadWord(read, header + AutoDataSegmentField, out int dgroup)) return false;
        if (!TryReadWord(read, header + SegmentCountField, out int count)) return false;
        if (!TryReadWord(read, header + SegmentTableField, out int table)) return false;
        if (!TryReadWord(read, header + AlignmentShiftField, out int shift)) return false;

        if (shift == 0) shift = DefaultAlignmentShift;
        if (shift > MaxAlignmentShift) return false;
        if (count < Seg3Number || dgroup < 1 || dgroup > count) return false;

        if (read(header + (long)table, count * RecordSize) is not { } records) return false;

        var bases = new int[count];
        for (int i = 0; i < count; i++)
        {
            bases[i] = BinaryPrimitives.ReadUInt16LittleEndian(records.AsSpan(i * RecordSize)) << shift;
        }

        layout = new MainExeLayout(bases, dgroup);
        return true;
    }

    private static bool TryReadWord(ReadAt read, long offset, out int value)
    {
        value = 0;
        if (read(offset, 2) is not { } word) return false;
        value = BinaryPrimitives.ReadUInt16LittleEndian(word);
        return true;
    }
}
