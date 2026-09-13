using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.MenuLine;

namespace DigItPatcher.Tests;

/// <summary>Tests resolving this tool's <c>MAIN.EXE</c> sites against a fabricated segment table, one shape per build.</summary>
public class MainExeLayoutTests
{
    private const int SlabCountTable = 0x10;

    /// <summary>Every site this tool reaches into <c>MAIN.EXE</c> for, and the file offset it is at in each build.</summary>
    public static TheoryData<string, int, int, int> Sites => new()
    {
        // name, seg3 address (0 for a DGROUP site), full release file offset, Manaccom edition file offset
        { "completion-% setter", 0xDD3B, 0x1893B, 0x18B3B },
        { "glyph page", GameFont.GlyphBlobSite, 0x18DB6, 0x18FB6 },
        { "CP437 translation table", GameFont.XlatSite, 0x19B58, 0x19D58 },
        { "menu line blit", MenuLineSources.Site, 0x18B1B, 0x18D1B },
    };

    [Theory]
    [MemberData(nameof(Sites))]
    public void EverySeg3SiteLandsWhereItsBuildPutsIt(string name, int site, int full, int manaccom)
    {
        Assert.Equal(full, LayoutOf(FakeMainExe.FullSeg3, FakeMainExe.FullDGroup).Seg3(site));
        Assert.Equal(manaccom, LayoutOf(FakeMainExe.ManaccomSeg3, FakeMainExe.ManaccomDGroup).Seg3(site));
        Assert.NotEqual(full, manaccom); // the name is carried only so a failure says which site broke
        Assert.NotEmpty(name);
    }

    [Fact]
    public void TheSlabCountTableLandsWhereItsBuildPutsIt()
    {
        Assert.Equal(0x20110, LayoutOf(FakeMainExe.FullSeg3, FakeMainExe.FullDGroup).DGroup(SlabCountTable));
        Assert.Equal(0x20310, LayoutOf(FakeMainExe.ManaccomSeg3, FakeMainExe.ManaccomDGroup).DGroup(SlabCountTable));
    }

    [Fact]
    public void AFileCarryingNoSegmentTableIsRefusedRatherThanGuessedAt()
    {
        Assert.False(MainExeLayout.TryRead(new byte[0x20000].AsSpan(), out _));
        Assert.False(MainExeLayout.TryRead(ReadOnlySpan<byte>.Empty, out _));

        var noSignature = FakeMainExe.FullRelease(0x1000);
        noSignature[0x80] = (byte)'M';
        Assert.False(MainExeLayout.TryRead(noSignature.AsSpan(), out _));
    }

    [Fact]
    public void ReadingFromAStreamAgreesWithReadingFromMemory()
    {
        var exe = FakeMainExe.WithSegments(FakeMainExe.ManaccomSeg3, FakeMainExe.ManaccomDGroup, 0x21000);

        Assert.True(MainExeLayout.TryRead(exe.AsSpan(), out var fromMemory));
        using var stream = new MemoryStream(exe);
        Assert.True(MainExeLayout.TryRead(stream, out var fromStream));

        Assert.Equal(fromMemory.Seg3(0), fromStream.Seg3(0));
        Assert.Equal(fromMemory.DGroup(0), fromStream.DGroup(0));
    }

    /// <summary>The copy in hand carries a segment table whose two segments sit in the recorded order.</summary>
    [Trait("Category", "RequiresGame")]
    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void TheInstalledExecutableCarriesASegmentTable()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));

        Assert.True(MainExeLayout.TryRead(install.ReadAll("MAIN.EXE"), out var layout));
        Assert.True(layout.Seg3(0) > 0);
        Assert.True(layout.DGroup(0) > layout.Seg3(0));
    }

    private static MainExeLayout LayoutOf(int seg3, int dgroup)
    {
        Assert.True(MainExeLayout.TryRead(FakeMainExe.WithSegments(seg3, dgroup, dgroup + 0x1000).AsSpan(), out var layout));
        return layout;
    }
}
