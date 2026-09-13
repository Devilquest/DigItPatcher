using System.Buffers.Binary;

namespace DigItPatcher.Core.Formats;

/// <summary>One of an archive's entries, with its data located in the archive as a whole.</summary>
/// <param name="Name">The entry's name, as the directory spells it.</param>
/// <param name="Start">Where the entry's data begins in the archive file.</param>
/// <param name="Length">How many bytes of data the entry declares.</param>
/// <param name="RecordAt">Where this entry's own 32-byte directory record begins.</param>
public sealed record XrsEntry(string Name, int Start, int Length, int RecordAt = 0)
{
    /// <summary>The first byte past this entry's data.</summary>
    public int End => Start + Length;

    /// <summary>Whether this entry holds one of the image formats, which are the ones that check themselves.</summary>
    public bool IsSheet => Name.EndsWith(".MPF", StringComparison.OrdinalIgnoreCase)
        || Name.EndsWith(".SPF", StringComparison.OrdinalIgnoreCase)
        || Name.EndsWith(".ANI", StringComparison.OrdinalIgnoreCase);
}

/// <summary>The directory at the head of one of the game's archives.</summary>
public static class XrsDirectory
{
    // u16 count, then 32-byte records: u8 name length, 12-byte name field, u32 offset, u32 length, 11 spare.
    private const int RecordSize = 32;
    private const int NameFieldSize = 12;

    /// <summary>Reads an archive's directory, or reports that these bytes do not open as one.</summary>
    public static bool TryRead(ReadOnlySpan<byte> archive, out IReadOnlyList<XrsEntry> entries)
    {
        entries = [];
        if (archive.Length < 2) return false;

        int count = BinaryPrimitives.ReadUInt16LittleEndian(archive);
        long dataStart = 2 + ((long)count * RecordSize);
        if (count == 0 || dataStart > archive.Length) return false;

        var read = new List<XrsEntry>(count);
        for (int i = 0; i < count; i++)
        {
            int recordAt = 2 + (i * RecordSize);
            var record = archive.Slice(recordAt, RecordSize);
            var name = ReadName(record);
            long start = dataStart + BinaryPrimitives.ReadUInt32LittleEndian(record[13..]);
            long length = BinaryPrimitives.ReadUInt32LittleEndian(record[17..]);
            if (start + length > archive.Length) return false;

            read.Add(new XrsEntry(name, (int)start, (int)length, recordAt));
        }

        entries = read;
        return true;
    }

    // The name field is fixed width and the length byte can overstate it, so the field is what caps the read.
    private static string ReadName(ReadOnlySpan<byte> record)
    {
        int length = Math.Min((int)record[0], NameFieldSize);
        Span<char> name = stackalloc char[length];
        int written = 0;
        for (int i = 0; i < length; i++)
        {
            byte c = record[1 + i];
            if (c < 0x80) name[written++] = (char)c;
        }

        return new string(name[..written]);
    }
}
