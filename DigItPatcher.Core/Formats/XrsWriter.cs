using System.Buffers.Binary;

namespace DigItPatcher.Core.Formats;

/// <summary>Appends a payload to an archive and repoints one entry's directory record to it.</summary>
public static class XrsWriter
{
    private const int RecordSize = 32;

    /// <summary>Returns the archive with <paramref name="payload"/> appended and the named entry pointed at it.</summary>
    public static byte[] Replace(byte[] archive, IReadOnlyList<XrsEntry> entries, string name, byte[] payload)
    {
        var entry = entries.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"no entry named {name}", nameof(name));

        int dataStart = 2 + (entries.Count * RecordSize);
        int relativeOffset = archive.Length - dataStart;

        var result = new byte[archive.Length + payload.Length];
        archive.CopyTo(result, 0);
        payload.CopyTo(result, archive.Length);

        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(entry.RecordAt + 13), (uint)relativeOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(entry.RecordAt + 17), (uint)payload.Length);

        return result;
    }
}
