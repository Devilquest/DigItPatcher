using System.Security.Cryptography;
using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.MenuLine;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the rebuilt archive and executable against the prototype's own write path.</summary>
public class MenuLineWriterTests
{
    // The prototype's own write over a verified DIGIT0.XRS and MAIN.EXE, composing "BUG FIXES 1.0",
    // hashes to these values; matching them is what validates the port.
    private const string ExpectedPageSha256 = "62c281640e58167d703560983ceb2c12e51e5adfdbcb5ef3a7d9a4bbb9e6c978";
    private const int ExpectedRows = 14;
    private const int ExpectedAnchor = 182;

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void TheRebuiltPageMatchesThePrototypesOwnWrite()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));
        Assert.True(MenuLineSources.Verify(install, out var sources, out var reason));

        var ink = MenuLineFont.ReadInk(sources!.Strip, MenuLineStrip.ReadCell(sources.Strip).Width, MenuLineStrip.OriginalHeight);
        var font = MenuLineFont.Glyphs(ink);
        var grown = MenuLineStrip.Compose(sources.Strip, font, ink.Background, ["BUG FIXES 1.0"]);
        var (archive, mainExe) = MenuLineWriter.Build(sources, grown.Strip, ExpectedAnchor);

        Assert.True(XrsDirectory.TryRead(archive, out var entries));
        var entry = entries.First(candidate => candidate.Name.Equals("CR.SPF", StringComparison.OrdinalIgnoreCase));
        var (_, chunks) = FrameContainer.Split(archive.AsSpan(entry.Start, entry.Length));
        var page = FrameContainer.DecodePages(chunks)[0];

        int siteAt = sources.Layout.Seg3(MenuLineSources.Site);
        Assert.Equal(ExpectedPageSha256, Sha256Of(page));
        Assert.Equal(ExpectedRows, mainExe[siteAt + MenuLineSources.BottomRowFromSite]);
        Assert.Equal(ExpectedAnchor, mainExe[siteAt + MenuLineSources.AnchorFromSite]);
    }

    private static string Sha256Of(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
