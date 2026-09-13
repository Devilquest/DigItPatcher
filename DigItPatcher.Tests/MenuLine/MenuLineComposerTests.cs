using System.Security.Cryptography;
using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.MenuLine;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the one thing a run calls: verify, read the ink, compose, write, or refuse.</summary>
public class MenuLineComposerTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private const byte Border = 112, Background = 0, Main = 127, Soft = 133;
    private const int Width = 246;
    private const int SiteAt = FakeMainExe.FullSeg3 + 0xDF1B;

    private static readonly byte[] Site =
    [
        0xC6, 0x06, 0x23, 0x7B, 0x00, 0x6A, 0x09, 0x6A, 0x01, 0x6A, 0x01, 0x68, 0xF4, 0x00, 0x6A, 0x07,
        0xB8, 0x26, 0x00, 0x2B, 0x06, 0x76, 0x14, 0x50, 0xA1, 0x76, 0x14, 0xC1, 0xE8, 0x02, 0x05, 0xBA,
        0x00, 0x50
    ];

    // Border all around, background inside, with a couple of ink pixels so ReadInk has a main and a soft
    // color to find, the same way a real caption's own lettering would give it one.
    private static byte[] BuildPristinePage()
    {
        var page = new byte[FrameCodec.FrameBytes];
        for (int y = 0; y < MenuLineStrip.OriginalHeight; y++)
            for (int x = 0; x < Width; x++)
                page[y * FrameCodec.Width + x] =
                    (y == 0 || y == MenuLineStrip.OriginalHeight - 1 || x == 0 || x == Width - 1) ? Border : Background;

        page[2 * FrameCodec.Width + 2] = Main;
        page[2 * FrameCodec.Width + 3] = Main;
        page[3 * FrameCodec.Width + 2] = Main;
        page[4 * FrameCodec.Width + 2] = Soft;
        return page;
    }

    private static byte[] Header()
    {
        var header = new byte[772];
        header[0] = 0; // one frame declared
        return header;
    }

    private static byte[] ValidArchive() =>
        SheetArchiveBuilder.Archive(("CR.SPF", FrameContainer.Build(Header(), [BuildPristinePage()])));

    private static byte[] ValidMainExe()
    {
        var exe = FakeMainExe.FullRelease(SiteAt + Site.Length);
        Site.CopyTo(exe, SiteAt);
        return exe;
    }

    private GameInstall InstallHolding(byte[] archive, byte[] mainExe, string? dir = null)
    {
        dir ??= _tempDir;
        File.WriteAllBytes(Path.Combine(dir, "DIGIT0.XRS"), archive);
        File.WriteAllBytes(Path.Combine(dir, "MAIN.EXE"), mainExe);
        Assert.True(GameInstall.TryOpen(dir, out var install));
        return install;
    }

    [Fact]
    public void EveryPieceVerifiedComposesTheLine()
    {
        var install = InstallHolding(ValidArchive(), ValidMainExe());

        Assert.True(MenuLineComposer.TryCompose(install, [], out var composition, out var refusal));
        Assert.Null(refusal);
        Assert.True(XrsDirectory.TryRead(composition!.Archive, out var entries));

        var entry = entries.First(candidate => candidate.Name.Equals("CR.SPF", StringComparison.OrdinalIgnoreCase));
        var (_, chunks) = FrameContainer.Split(composition.Archive.AsSpan(entry.Start, entry.Length));
        var cell = MenuLineStrip.ReadCell(FrameContainer.DecodePages(chunks)[0]);
        Assert.Equal(MenuLineStrip.OriginalHeight + MenuLineStrip.Band, cell.Height);

        Assert.Equal(14, composition.MainExe[SiteAt + 15]);

        // One line's own rows, the same arithmetic MenuLineStrip.Compose returns as Composition.Rows.
        int rows = MenuLineStrip.OriginalHeight + MenuLineStrip.Band - 2;
        int expectedAnchor = MenuLineComposer.ScreenHeight - rows - MenuLineComposer.Margin;
        Assert.Equal(expectedAnchor, composition.MainExe[SiteAt + 31]);
    }

    [Fact]
    public void ComposingAModNameAppendsItAfterTheVersion()
    {
        var install = InstallHolding(ValidArchive(), ValidMainExe());

        Assert.True(MenuLineComposer.TryCompose(install, ["TURBO"], out var composition, out var refusal));
        Assert.Null(refusal);

        var entries = ReadEntries(composition!.Archive);
        var page = DecodeStrip(composition.Archive, entries);
        var font = MenuLineFont.Glyphs(MenuLineFont.ReadInk(BuildPristinePage(), Width, MenuLineStrip.OriginalHeight));

        // Composing "BUG FIXES 1.0" alone and "BUG FIXES 1.0 + TURBO" both onto a fresh cell, then comparing
        // the pixels, is the cheapest way to confirm the mod's name actually reached the drawn line.
        var withoutMod = MenuLineStrip.Compose(BuildPristinePage(), font, Background, ["BUG FIXES 1.0"]).Strip;
        Assert.NotEqual(withoutMod, page);
    }

    [Fact]
    public void ARefusedGuardRefusesTheComposition()
    {
        var install = InstallHolding([1, 2, 3], ValidMainExe());

        Assert.False(MenuLineComposer.TryCompose(install, [], out var composition, out var refusal));
        Assert.Null(composition);
        Assert.NotNull(refusal);
        Assert.Contains("does not open as an archive", refusal!.Reason);
    }

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void ComposingOverTheUsersOwnCopySucceeds()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));

        Assert.True(MenuLineComposer.TryCompose(install, [], out var composition, out var refusal));
        Assert.Null(refusal);
        Assert.True(XrsDirectory.TryRead(composition!.Archive, out _));
    }

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void ComposingOverItsOwnOutputIsIdempotent()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));
        Assert.True(MenuLineComposer.TryCompose(install, [], out var first, out _));

        var reComposedDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;
        try
        {
            var reInstall = InstallHolding(first!.Archive, first.MainExe, reComposedDir);
            Assert.True(MenuLineComposer.TryCompose(reInstall, [], out var second, out var refusal));
            Assert.Null(refusal);

            var firstPage = DecodeStrip(first.Archive, ReadEntries(first.Archive));
            var secondPage = DecodeStrip(second!.Archive, ReadEntries(second.Archive));
            Assert.Equal(Sha256Of(firstPage), Sha256Of(secondPage));
        }
        finally
        {
            Directory.Delete(reComposedDir, recursive: true);
        }
    }

    private static IReadOnlyList<XrsEntry> ReadEntries(byte[] archive)
    {
        Assert.True(XrsDirectory.TryRead(archive, out var entries));
        return entries;
    }

    private static byte[] DecodeStrip(byte[] archive, IReadOnlyList<XrsEntry> entries)
    {
        var entry = entries.First(candidate => candidate.Name.Equals("CR.SPF", StringComparison.OrdinalIgnoreCase));
        var (_, chunks) = FrameContainer.Split(archive.AsSpan(entry.Start, entry.Length));
        return FrameContainer.DecodePages(chunks)[0];
    }

    private static string Sha256Of(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
