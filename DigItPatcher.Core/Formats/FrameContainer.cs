using System.Buffers.Binary;

namespace DigItPatcher.Core.Formats;

/// <summary>An image entry split into its 772-byte header and the compressed chunk for each page.</summary>
public static class FrameContainer
{
    // u16 frame count past the first, u16 offset of the second frame, then a 768-byte palette.
    private const int HeaderSize = 772;

    /// <summary>Splits an entry into its header and the compressed chunk for each page it declares.</summary>
    public static (byte[] Header, List<byte[]> Chunks) Split(ReadOnlySpan<byte> entry)
    {
        var chunks = new List<byte[]>();
        int p = HeaderSize;
        while (p + 2 <= entry.Length)
        {
            int size = BinaryPrimitives.ReadUInt16LittleEndian(entry[p..]);
            if (size == 0 || p + 2 + size > entry.Length) break;
            chunks.Add(entry.Slice(p + 2, size).ToArray());
            p += 2 + size;
        }

        return (entry[..HeaderSize].ToArray(), chunks);
    }

    /// <summary>Chain-decodes every page: a page whose stream never touches a byte leaves it holding the page before it.</summary>
    public static List<byte[]> DecodePages(IReadOnlyList<byte[]> chunks)
    {
        var working = new byte[FrameCodec.WorkingSize];
        var pages = new List<byte[]>(chunks.Count);
        foreach (var chunk in chunks)
        {
            FrameCodec.Decompress(chunk, working);
            pages.Add(working[..FrameCodec.FrameBytes]);
        }

        return pages;
    }

    /// <summary>Re-encodes every page, letting <paramref name="keep"/> supply the original chunk for a page left untouched.</summary>
    public static byte[] Build(ReadOnlySpan<byte> header, IReadOnlyList<byte[]> pages, IReadOnlyDictionary<int, byte[]>? keep = null)
    {
        var chunks = new byte[pages.Count][];
        for (int i = 0; i < pages.Count; i++)
            chunks[i] = keep != null && keep.TryGetValue(i, out var original) ? original : FrameCodec.Compress(pages[i]);

        var result = new byte[HeaderSize + chunks.Sum(chunk => 2 + chunk.Length)];
        header.CopyTo(result);

        // The second frame's offset is derived from the first frame's size, so it moves whenever page 0 is re-encoded.
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), (ushort)(HeaderSize + 2 + chunks[0].Length));

        int at = HeaderSize;
        foreach (var chunk in chunks)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(at), (ushort)chunk.Length);
            chunk.CopyTo(result.AsSpan(at + 2));
            at += 2 + chunk.Length;
        }

        return result;
    }
}
