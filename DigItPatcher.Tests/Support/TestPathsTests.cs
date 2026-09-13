using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Tests;

/// <summary>Tests holding that the game is looked for inside this repository and nowhere above it.</summary>
public class TestPathsTests
{
    /// <summary>The last folder searched is the repository root, and every other one is inside it.</summary>
    [Fact]
    public void TheSearchForTheGameStopsAtTheRepositoryRoot()
    {
        var scope = TestPaths.SearchScope();

        Assert.NotEmpty(scope);

        var root = scope[^1];
        Assert.True(File.Exists(Path.Combine(root.FullName, "DigItPatcher.slnx")),
            $"the last folder searched should hold the solution file, and it was {root.FullName}");
        Assert.All(scope, dir =>
            Assert.StartsWith(root.FullName, dir.FullName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Strict mode asks for a failure, so it must never produce a skip instead.</summary>
    [Fact]
    public void StrictModeNeverTurnsAMissingInstallIntoASkip()
    {
        Assert.NotNull(TestPaths.SkipReason(gameFound: false, strict: false));
        Assert.Null(TestPaths.SkipReason(gameFound: false, strict: true));
        Assert.Null(TestPaths.SkipReason(gameFound: true, strict: false));
    }

    /// <summary>A copy that is a build this tool was never derived against is a skip, never a failure.</summary>
    [Fact]
    public void OnlyABuildThisToolWasDerivedAgainstRuns()
    {
        Assert.Null(TestPaths.UnsupportedBuildSkipReason(new KnownBuild("full", "Full release")));
        Assert.Null(TestPaths.UnsupportedBuildSkipReason(new KnownBuild("manaccom", "Full release, Manaccom edition")));
        Assert.NotNull(TestPaths.UnsupportedBuildSkipReason(new KnownBuild("shareware", "Shareware release")));
        Assert.NotNull(TestPaths.UnsupportedBuildSkipReason(null));
    }

    /// <summary>A copy whose executable cannot be read is a skip, never a failure, whichever build it names.</summary>
    [Fact]
    public void AnExecutableThatCannotBeReadIsASkip()
    {
        Assert.Null(TestPaths.UnreadableExecutableSkipReason(readsAsAnExecutable: true));
        Assert.NotNull(TestPaths.UnreadableExecutableSkipReason(readsAsAnExecutable: false));
    }

    /// <summary>An executable holding its table but not what the table points at is short of the data segment.</summary>
    [Fact]
    public void ATruncatedExecutableStopsBeforeItsDataSegment()
    {
        var whole = FakeMainExe.FullRelease(FakeMainExe.FullDGroup + 0x1000);
        var truncated = whole.AsSpan(0, 4000).ToArray();

        // Both read: the table sits in the first few hundred bytes, far below the segment it points at,
        // which is what lets a truncated download hand one back.
        Assert.True(MainExeLayout.TryRead(whole.AsSpan(), out var fromWhole));
        Assert.True(MainExeLayout.TryRead(truncated.AsSpan(), out var fromTruncated));

        Assert.True(whole.Length > fromWhole.DGroup(0));
        Assert.False(truncated.Length > fromTruncated.DGroup(0));
    }

    /// <summary>The gate names builds the release list holds, so renaming one cannot silently skip the suite.</summary>
    [Fact]
    public void TheGateNamesBuildsTheReleaseListHolds()
    {
        var known = ReleaseCatalog.Embedded.Builds.Select(build => build.Id);

        Assert.All(TestPaths.SupportedBuilds, id => Assert.Contains(id, known));
    }
}
