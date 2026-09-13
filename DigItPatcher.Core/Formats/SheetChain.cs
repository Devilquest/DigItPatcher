using System.Buffers.Binary;

namespace DigItPatcher.Core.Formats;

/// <summary>What a walk over an image entry's frame chain found, and where it stopped if it stopped early.</summary>
/// <param name="IsIntact">Whether every frame the header declares walked and decoded whole.</param>
/// <param name="FaultStart">Where in the entry a byte would have to be wrong for the walk to end as it did.</param>
/// <param name="FaultEnd">The first byte past that range.</param>
public readonly record struct SheetWalk(bool IsIntact, int FaultStart, int FaultEnd);

/// <summary>The frame chain an image entry carries, walked far enough to tell an intact entry from a displaced one.</summary>
public static class SheetChain
{
    // u16 frame count past the first, u16 offset of the second frame, then a 768-byte palette.
    private const int HeaderSize = 772;

    /// <summary>Walks an entry's frame chain, reporting where it stopped agreeing with itself.</summary>
    public static SheetWalk Walk(ReadOnlySpan<byte> entry, byte[] working)
    {
        if (entry.Length < HeaderSize) return new SheetWalk(false, 0, entry.Length);

        int declared = BinaryPrimitives.ReadUInt16LittleEndian(entry) + 1;
        int secondFrameAt = BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]);

        int at = HeaderSize;
        int previousFrame = HeaderSize;

        for (int i = 0; i < declared; i++)
        {
            if (at + 2 > entry.Length) return new SheetWalk(false, previousFrame, entry.Length);

            int size = BinaryPrimitives.ReadUInt16LittleEndian(entry[at..]);
            int data = at + 2;

            // A size that does not fit is as likely to be a displaced size field as a displaced frame before it,
            // so the fault covers the previous frame's data and this header both.
            if (size == 0 || data + size > entry.Length) return new SheetWalk(false, previousFrame, entry.Length);

            // The second frame's offset is derived from the first frame's size, so the two disagree the moment
            // either one is read from the wrong place.
            if (i == 0 && secondFrameAt != HeaderSize + 2 + size) return new SheetWalk(false, HeaderSize, data + size);

            if (!FrameCodec.IsIntact(entry.Slice(data, size), working)) return new SheetWalk(false, data, data + size);

            previousFrame = data;
            at = data + size;
        }

        // An entry that walks its frames and then has bytes left over did not walk the entry it declared itself to be.
        return at == entry.Length ? new SheetWalk(true, 0, 0) : new SheetWalk(false, previousFrame, entry.Length);
    }
}
