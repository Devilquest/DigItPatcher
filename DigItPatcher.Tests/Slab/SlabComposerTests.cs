using DigItPatcher.Core;
using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Slab;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the one thing a run calls: verify, derive, paint, write, or refuse.</summary>
public class SlabComposerTests : IDisposable
{
    // DS:0x10, one word per screen: instructions, then the credits screen.
    private const int SlabCountTable = 0x10;
    private const int CreditsScreenIndex = 1;

    private readonly string _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    private static readonly AppIdentity TestIdentity = new("Dig It! Patcher", "1.0.0-test", "Devilquest");

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void ComposingOverTheUsersOwnCopySucceedsWithNoLinesToOverflow()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));

        Assert.True(SlabComposer.TryCompose(install, TestIdentity, [], out var composition, out var refusal));
        Assert.Null(refusal);
        Assert.True(XrsDirectory.TryRead(composition!.Archive, out _));

        Assert.True(MainExeLayout.TryRead(composition.MainExe, out var layout));
        int at = layout.DGroup(SlabCountTable + (CreditsScreenIndex * 2));
        int slabCount = composition.MainExe[at] | (composition.MainExe[at + 1] << 8);
        Assert.Equal(2, slabCount);
    }

    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void ComposingWithEveryShippedFixesLineFitsTheStone()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));

        Assert.True(SlabComposer.TryCompose(install, TestIdentity, FixCatalog.All, out var composition, out var refusal));
        Assert.Null(refusal);
        Assert.True(XrsDirectory.TryRead(composition!.Archive, out _));
    }

    [Fact]
    public void ARefusedGuardRefusesTheComposition()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "DIGIT0.XRS"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_tempDir, "MAIN.EXE"), [0]);
        Assert.True(GameInstall.TryOpen(_tempDir, out var install));

        Assert.False(SlabComposer.TryCompose(install, TestIdentity, [], out var composition, out var refusal));
        Assert.Null(composition);
        Assert.NotNull(refusal);
        Assert.Contains("does not open as an archive", refusal!.Reason);
    }
}
