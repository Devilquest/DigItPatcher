using System.Security.Cryptography;
using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Slab;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the rebuilt archive and executable against the prototype's own write path.</summary>
public class SlabWriterTests
{
    // DS:0x10, one word per screen: instructions, then the credits screen.
    private const int SlabCountTable = 0x10;
    private const int CreditsScreenIndex = 1;

    // The prototype's own write over a verified DIGIT0.XRS, on the blank strip SlabBlankTests already
    // validated, hashes to these values; matching them is what validates the port.
    private const string ExpectedForegroundSha256 = "918ec11a9035260fee121fe3c0ff5d8725df3c8e0705449e46322c7631053206";
    private const string ExpectedMaskSha256 = "7742345d906edfda94795db090b1f3fb641de1359021064010817f926c774a69";

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void TheRebuiltArchiveMatchesThePrototypesOwnWrite()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));
        Assert.True(SlabSources.Verify(install, out var sources, out var reason));

        var blank = SlabBlank.Derive(sources!.Art, sources.Donor, sources.Palette);
        var (archive, _) = SlabWriter.Build(sources, blank);

        Assert.True(XrsDirectory.TryRead(archive, out var entries));
        Assert.Equal(ExpectedForegroundSha256, Sha256Of(EntryBytes(archive, entries, "SLB01F.MPF")));
        Assert.Equal(ExpectedMaskSha256, Sha256Of(EntryBytes(archive, entries, "SLB01M.MPF")));
    }

    /// <summary>The write reaches the two entries it rebuilds and no others, whichever copy it ran over.</summary>
    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void NothingButTheTwoRebuiltEntriesChanges()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));
        Assert.True(SlabSources.Verify(install, out var sources, out _));

        var blank = SlabBlank.Derive(sources!.Art, sources.Donor, sources.Palette);
        var (archive, _) = SlabWriter.Build(sources, blank);

        Assert.True(XrsDirectory.TryRead(archive, out var after));
        Assert.Equal(sources.Entries.Select(entry => entry.Name), after.Select(entry => entry.Name));

        string[] rebuilt = ["SLB01F.MPF", "SLB01M.MPF"];
        var untouched = after.Where(entry => !rebuilt.Contains(entry.Name, StringComparer.OrdinalIgnoreCase));
        Assert.NotEmpty(untouched);
        Assert.All(untouched, entry => Assert.Equal(
            Sha256Of(EntryBytes(sources.Archive, sources.Entries, entry.Name)),
            Sha256Of(EntryBytes(archive, after, entry.Name))));
    }

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void TheSlabCountWordReadsThreeSlabsRegardlessOfWhereItStarted()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));
        Assert.True(SlabSources.Verify(install, out var sources, out _));

        var blank = SlabBlank.Derive(sources!.Art, sources.Donor, sources.Palette);
        var (_, mainExe) = SlabWriter.Build(sources, blank);

        int at = sources.Layout.DGroup(SlabCountTable + (CreditsScreenIndex * 2));
        int slabCount = mainExe[at] | (mainExe[at + 1] << 8);
        Assert.Equal(2, slabCount); // 3 slabs stored as slabs - 1
    }

    private static byte[] EntryBytes(byte[] archive, IReadOnlyList<XrsEntry> entries, string name)
    {
        var entry = entries.First(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return archive[entry.Start..entry.End];
    }

    private static string Sha256Of(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
