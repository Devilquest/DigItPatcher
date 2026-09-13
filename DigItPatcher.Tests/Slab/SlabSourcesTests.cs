using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Slab;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the guard: one per check, each fired by an archive damaged on purpose.</summary>
public class SlabSourcesTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static byte[] ValidArchive() => SheetArchiveBuilder.Archive(
        ("SLB00F.MPF", SheetArchiveBuilder.Sheet(8)),
        ("SLB01F.MPF", SheetArchiveBuilder.Sheet(8, fill: 20)),
        ("SLB01M.MPF", SheetArchiveBuilder.Sheet(8, fill: 40)),
        ("SLB00.PAL", RawPalette(10, 10, 10)),
        ("SLB01.PAL", RawPalette(10, 10, 10)));

    private static byte[] RawPalette(byte r, byte g, byte b)
    {
        var pal = new byte[768];
        for (int i = 0; i < 256; i++)
        {
            pal[i * 3] = r;
            pal[(i * 3) + 1] = g;
            pal[(i * 3) + 2] = b;
        }

        return pal;
    }

    private static byte[] ValidMainExe(int slabCount = 1)
        => ValidMainExe(FakeMainExe.FullSeg3, FakeMainExe.FullDGroup, slabCount);

    private static byte[] ValidMainExe(int seg3, int dgroup, int slabCount = 1)
    {
        var exe = FakeMainExe.WithSegments(seg3, dgroup, dgroup + 0x14);
        var blob = FrameContainer.Build(new byte[772], [new byte[FrameCodec.FrameBytes]]);
        blob.CopyTo(exe, seg3 + GameFont.GlyphBlobSite);

        int at = dgroup + 0x10 + 2; // credits screen, DS:0x10 + 1 * 2
        exe[at] = (byte)slabCount;
        exe[at + 1] = 0;
        return exe;
    }

    private GameInstall InstallHolding(byte[] archive, byte[] mainExe)
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "DIGIT0.XRS"), archive);
        File.WriteAllBytes(Path.Combine(_tempDir, "MAIN.EXE"), mainExe);
        Assert.True(GameInstall.TryOpen(_tempDir, out var install));
        return install;
    }

    [Fact]
    public void EveryPieceVerifiedYieldsTheDecodedSources()
    {
        var install = InstallHolding(ValidArchive(), ValidMainExe());

        Assert.True(SlabSources.Verify(install, out var sources, out var reason));
        Assert.Null(reason);
        Assert.Equal(8 * FrameCodec.FrameBytes, sources!.Art.Length);
        Assert.Equal(8 * FrameCodec.FrameBytes, sources.Donor.Length);
        Assert.Equal(256, sources.Palette.Length);
    }

    [Fact]
    public void AnArchiveThatDoesNotOpenIsRefused()
    {
        var install = InstallHolding([1, 2, 3], ValidMainExe());

        Assert.False(SlabSources.Verify(install, out var sources, out var reason));
        Assert.Null(sources);
        Assert.Contains("does not open as an archive", reason);
    }

    [Fact]
    public void AMissingEntryIsRefused()
    {
        var archive = SheetArchiveBuilder.Archive(
            ("SLB00F.MPF", SheetArchiveBuilder.Sheet(8)),
            ("SLB01F.MPF", SheetArchiveBuilder.Sheet(8, fill: 20)),
            ("SLB00.PAL", RawPalette(10, 10, 10)),
            ("SLB01.PAL", RawPalette(10, 10, 10)));
        var install = InstallHolding(archive, ValidMainExe());

        Assert.False(SlabSources.Verify(install, out _, out var reason));
        Assert.Contains("SLB01M.MPF is missing", reason);
    }

    [Fact]
    public void ADamagedImageEntryIsRefused()
    {
        var archive = SheetArchiveBuilder.Archive(
            ("SLB00F.MPF", SheetArchiveBuilder.Sheet(8)),
            ("SLB01F.MPF", SheetArchiveBuilder.WithByteInserted(SheetArchiveBuilder.Sheet(8, fill: 20), 772 + 3, 0xF4)),
            ("SLB01M.MPF", SheetArchiveBuilder.Sheet(8, fill: 40)),
            ("SLB00.PAL", RawPalette(10, 10, 10)),
            ("SLB01.PAL", RawPalette(10, 10, 10)));
        var install = InstallHolding(archive, ValidMainExe());

        Assert.False(SlabSources.Verify(install, out _, out var reason));
        Assert.Contains("SLB01F.MPF did not decode intact", reason);
    }

    [Fact]
    public void AWrongPageCountIsRefused()
    {
        var archive = SheetArchiveBuilder.Archive(
            ("SLB00F.MPF", SheetArchiveBuilder.Sheet(8)),
            ("SLB01F.MPF", SheetArchiveBuilder.Sheet(5, fill: 20)),
            ("SLB01M.MPF", SheetArchiveBuilder.Sheet(8, fill: 40)),
            ("SLB00.PAL", RawPalette(10, 10, 10)),
            ("SLB01.PAL", RawPalette(10, 10, 10)));
        var install = InstallHolding(archive, ValidMainExe());

        Assert.False(SlabSources.Verify(install, out _, out var reason));
        Assert.Contains("SLB01F.MPF decodes to 5 pages", reason);
    }

    [Fact]
    public void MismatchedPalettesAreRefused()
    {
        var archive = SheetArchiveBuilder.Archive(
            ("SLB00F.MPF", SheetArchiveBuilder.Sheet(8)),
            ("SLB01F.MPF", SheetArchiveBuilder.Sheet(8, fill: 20)),
            ("SLB01M.MPF", SheetArchiveBuilder.Sheet(8, fill: 40)),
            ("SLB00.PAL", RawPalette(10, 10, 10)),
            ("SLB01.PAL", RawPalette(20, 20, 20)));
        var install = InstallHolding(archive, ValidMainExe());

        Assert.False(SlabSources.Verify(install, out _, out var reason));
        Assert.Contains("are not the same palette", reason);
    }

    [Fact]
    public void AMissingFontIsRefused()
    {
        var install = InstallHolding(ValidArchive(), FakeMainExe.FullRelease(FakeMainExe.FullDGroup + 0x14));

        Assert.False(SlabSources.Verify(install, out _, out var reason));
        Assert.Contains("does not hold a font", reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void AnUnrecognizedSlabCountIsRefused(int slabCount)
    {
        var install = InstallHolding(ValidArchive(), ValidMainExe(slabCount));

        Assert.False(SlabSources.Verify(install, out _, out var reason));
        Assert.Contains("not 1 or 2", reason);
    }

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void TheGuardPassesOnTheUsersOwnCopy()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));

        Assert.True(SlabSources.Verify(install, out var sources, out var reason));
        Assert.Null(reason);
        Assert.Equal(8 * FrameCodec.FrameBytes, sources!.Art.Length);
    }
}
