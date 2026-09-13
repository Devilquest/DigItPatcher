using DigItPatcher.Core.Formats;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the directory an archive carries at its head.</summary>
public class XrsDirectoryTests
{
    [Fact]
    public void EveryEntryComesBackWithItsNameAndItsBytes()
    {
        var first = SheetArchiveBuilder.Sheet(1);
        var second = SheetArchiveBuilder.Sheet(2);
        var archive = SheetArchiveBuilder.Archive(("LVL001M.MPF", first), ("LVL002F.MPF", second));

        Assert.True(XrsDirectory.TryRead(archive, out var entries));

        Assert.Equal(["LVL001M.MPF", "LVL002F.MPF"], entries.Select(entry => entry.Name));
        Assert.Equal(first, archive[entries[0].Start..entries[0].End]);
        Assert.Equal(second, archive[entries[1].Start..entries[1].End]);
    }

    [Fact]
    public void AnEntryReachingPastTheEndOfTheArchiveIsRefused()
    {
        var archive = SheetArchiveBuilder.Archive(("LVL001M.MPF", SheetArchiveBuilder.Sheet(1)));

        Assert.False(XrsDirectory.TryRead(archive[..^1], out _));
    }

    [Theory]
    [InlineData("LVL001M.MPF", true)]
    [InlineData("LVL001.PAL", false)]
    [InlineData("TUNE01.DAT", false)]
    public void OnlyTheImageFormatsAreTreatedAsCheckingThemselves(string name, bool isSheet)
        => Assert.Equal(isSheet, new XrsEntry(name, 0, 0).IsSheet);
}
