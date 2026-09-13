using System.Buffers.Binary;
using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.MenuLine;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the menu line's guard: one per check, each fired by an archive or executable
/// damaged on purpose.</summary>
public class MenuLineSourcesTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private const byte Border = 112, Background = 0;
    private const int Width = 246;
    private const int SiteAt = FakeMainExe.FullSeg3 + 0xDF1B;

    private static readonly byte[] Site =
    [
        0xC6, 0x06, 0x23, 0x7B, 0x00, 0x6A, 0x09, 0x6A, 0x01, 0x6A, 0x01, 0x68, 0xF4, 0x00, 0x6A, 0x07,
        0xB8, 0x26, 0x00, 0x2B, 0x06, 0x76, 0x14, 0x50, 0xA1, 0x76, 0x14, 0xC1, 0xE8, 0x02, 0x05, 0xBA,
        0x00, 0x50
    ];

    private static byte[] BuildPristinePage()
    {
        var page = new byte[FrameCodec.FrameBytes];
        for (int y = 0; y < MenuLineStrip.OriginalHeight; y++)
            for (int x = 0; x < Width; x++)
                page[y * FrameCodec.Width + x] =
                    (y == 0 || y == MenuLineStrip.OriginalHeight - 1 || x == 0 || x == Width - 1) ? Border : Background;
        return page;
    }

    private static byte[] Header(int frameCount)
    {
        var header = new byte[772];
        BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)(frameCount - 1));
        return header;
    }

    private static byte[] ValidArchive(byte[]? page = null) =>
        SheetArchiveBuilder.Archive(("CR.SPF", FrameContainer.Build(Header(1), [page ?? BuildPristinePage()])));

    private static byte[] ValidMainExe()
    {
        var exe = FakeMainExe.FullRelease(SiteAt + Site.Length);
        Site.CopyTo(exe, SiteAt);
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

        Assert.True(MenuLineSources.Verify(install, out var sources, out var reason));
        Assert.Null(reason);
        Assert.Equal(FrameCodec.FrameBytes, sources!.Strip.Length);
    }

    [Fact]
    public void AnArchiveThatDoesNotOpenIsRefused()
    {
        var install = InstallHolding([1, 2, 3], ValidMainExe());

        Assert.False(MenuLineSources.Verify(install, out var sources, out var reason));
        Assert.Null(sources);
        Assert.Contains("does not open as an archive", reason);
    }

    [Fact]
    public void AMissingStripIsRefused()
    {
        var archive = SheetArchiveBuilder.Archive(("OTHER.SPF", FrameContainer.Build(Header(1), [BuildPristinePage()])));
        var install = InstallHolding(archive, ValidMainExe());

        Assert.False(MenuLineSources.Verify(install, out _, out var reason));
        Assert.Contains("CR.SPF is missing", reason);
    }

    [Fact]
    public void ADamagedStripIsRefused()
    {
        var whole = FrameContainer.Build(Header(1), [BuildPristinePage()]);
        var damaged = SheetArchiveBuilder.WithByteInserted(whole, 772 + 3, 0xF4);
        var install = InstallHolding(SheetArchiveBuilder.Archive(("CR.SPF", damaged)), ValidMainExe());

        Assert.False(MenuLineSources.Verify(install, out _, out var reason));
        Assert.Contains("CR.SPF did not decode intact", reason);
    }

    [Fact]
    public void AStripWithMoreThanOnePageIsRefused()
    {
        var archive = SheetArchiveBuilder.Archive(
            ("CR.SPF", FrameContainer.Build(Header(2), [BuildPristinePage(), BuildPristinePage()])));
        var install = InstallHolding(archive, ValidMainExe());

        Assert.False(MenuLineSources.Verify(install, out _, out var reason));
        Assert.Contains("decodes to 2 pages, not 1", reason);
    }

    [Fact]
    public void AStripThatIsNotAValidCellIsRefused()
    {
        var install = InstallHolding(ValidArchive(new byte[FrameCodec.FrameBytes]), ValidMainExe());

        Assert.False(MenuLineSources.Verify(install, out _, out var reason));
        Assert.Contains("not a cell this tool can rebuild from", reason);
    }

    [Fact]
    public void AnExecutableThatDoesNotCarryTheSiteIsRefused()
    {
        var exe = ValidMainExe();
        exe[SiteAt] = 0;
        var install = InstallHolding(ValidArchive(), exe);

        Assert.False(MenuLineSources.Verify(install, out _, out var reason));
        Assert.Contains("does not carry the blit site", reason);
    }

    [Fact]
    public void AnExecutableAlreadyCarryingALineStillVerifies()
    {
        var exe = ValidMainExe();
        exe[SiteAt + 15] = 14;
        exe[SiteAt + 31] = 150;
        var install = InstallHolding(ValidArchive(), exe);

        Assert.True(MenuLineSources.Verify(install, out _, out var reason));
        Assert.Null(reason);
    }

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void TheGuardPassesOnTheUsersOwnCopy()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));

        Assert.True(MenuLineSources.Verify(install, out var sources, out var reason));
        Assert.Null(reason);
        Assert.Equal(FrameCodec.FrameBytes, sources!.Strip.Length);
    }
}
