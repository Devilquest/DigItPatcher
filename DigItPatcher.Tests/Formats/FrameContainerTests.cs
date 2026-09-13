using System.Buffers.Binary;
using DigItPatcher.Core.Formats;

namespace DigItPatcher.Tests;

/// <summary>Tests covering an entry split into pages, rebuilt, and decoded again.</summary>
public class FrameContainerTests
{
    [Fact]
    public void SplitAndDecodeRecoverTheSamePagesTheEntryWasBuiltFrom()
    {
        var entry = SheetArchiveBuilder.Sheet(3);
        var (_, chunks) = FrameContainer.Split(entry);
        var pages = FrameContainer.DecodePages(chunks);

        Assert.Equal(3, pages.Count);
        foreach (var page in pages) Assert.Equal(FrameCodec.FrameBytes, page.Length);
    }

    [Fact]
    public void ARebuildWithOnePageChangedDecodesBackToWhatWasAsked()
    {
        var entry = SheetArchiveBuilder.Sheet(3);
        var (header, chunks) = FrameContainer.Split(entry);
        var pages = FrameContainer.DecodePages(chunks);

        var changed = new byte[FrameCodec.FrameBytes];
        Array.Fill(changed, (byte)200);
        pages[1] = changed;

        var keep = new Dictionary<int, byte[]> { [0] = chunks[0], [2] = chunks[2] };
        var rebuilt = FrameContainer.Build(header, pages, keep);

        var (rebuiltHeader, rebuiltChunks) = FrameContainer.Split(rebuilt);
        var rebuiltPages = FrameContainer.DecodePages(rebuiltChunks);

        Assert.Equal(pages[0], rebuiltPages[0]);
        Assert.Equal(changed, rebuiltPages[1]);
        Assert.Equal(pages[2], rebuiltPages[2]);
        Assert.Equal(header[4..], rebuiltHeader[4..]); // everything but the second-frame offset word is untouched
    }

    [Fact]
    public void RebuildingPageZeroMovesTheSecondFrameOffsetWord()
    {
        var entry = SheetArchiveBuilder.Sheet(2);
        var (header, chunks) = FrameContainer.Split(entry);
        var pages = FrameContainer.DecodePages(chunks);

        var rebuilt = FrameContainer.Build(header, pages);

        int declaredOffset = BinaryPrimitives.ReadUInt16LittleEndian(rebuilt.AsSpan(2));
        int firstChunkSize = BinaryPrimitives.ReadUInt16LittleEndian(rebuilt.AsSpan(772));
        Assert.Equal(772 + 2 + firstChunkSize, declaredOffset);
    }
}
