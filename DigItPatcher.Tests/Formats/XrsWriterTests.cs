using DigItPatcher.Core.Formats;

namespace DigItPatcher.Tests;

/// <summary>Tests covering an entry appended to an archive and its directory record repointed.</summary>
public class XrsWriterTests
{
    [Fact]
    public void AnEntryAppendedAndRepointedIsFoundAgainAtItsNewLength()
    {
        var archive = SheetArchiveBuilder.Archive(("SLB01F.MPF", SheetArchiveBuilder.Sheet(2)));
        Assert.True(XrsDirectory.TryRead(archive, out var entries));

        var payload = SheetArchiveBuilder.Sheet(3);
        var rebuilt = XrsWriter.Replace(archive, entries, "SLB01F.MPF", payload);

        Assert.True(XrsDirectory.TryRead(rebuilt, out var rebuiltEntries));
        var entry = Assert.Single(rebuiltEntries, candidate => candidate.Name == "SLB01F.MPF");
        Assert.Equal(payload.Length, entry.Length);
        Assert.Equal(payload, rebuilt[entry.Start..entry.End]);
    }

    [Fact]
    public void ReplacingOneEntryLeavesTheOthersReadableAtTheirOriginalBytes()
    {
        var archive = SheetArchiveBuilder.Archive(
            ("SLB01F.MPF", SheetArchiveBuilder.Sheet(2)),
            ("SLB01M.MPF", SheetArchiveBuilder.Sheet(2)));
        Assert.True(XrsDirectory.TryRead(archive, out var entries));
        var originalMask = archive[entries[1].Start..entries[1].End];

        var rebuilt = XrsWriter.Replace(archive, entries, "SLB01F.MPF", SheetArchiveBuilder.Sheet(4));

        Assert.True(XrsDirectory.TryRead(rebuilt, out var rebuiltEntries));
        var mask = Assert.Single(rebuiltEntries, candidate => candidate.Name == "SLB01M.MPF");
        Assert.Equal(originalMask, rebuilt[mask.Start..mask.End]);
    }

    [Fact]
    public void AnUnknownNameIsRefused()
    {
        var archive = SheetArchiveBuilder.Archive(("SLB01F.MPF", SheetArchiveBuilder.Sheet(1)));
        Assert.True(XrsDirectory.TryRead(archive, out var entries));

        Assert.Throws<ArgumentException>(() => XrsWriter.Replace(archive, entries, "NOPE.MPF", []));
    }
}
